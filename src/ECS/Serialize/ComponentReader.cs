﻿// Copyright (c) Ullrich Praetz. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Friflo.Json.Burst;
using Friflo.Json.Fliox;
using Friflo.Json.Fliox.Mapper;

// ReSharper disable CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
// ReSharper disable InlineTemporaryVariable
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
namespace Friflo.Fliox.Engine.ECS.Serialize;

/// <summary>
/// Create all components / scripts for an entity from <see cref="JsonValue"/> used as <see cref="DataEntity.components"/>
/// </summary>
internal sealed class ComponentReader
{
    private readonly    ObjectReader                            componentReader;
    private readonly    Dictionary<string, SchemaType>          schemaTypeByKey;
    private readonly    Dictionary<Type,   ScriptType>          scriptTypeByType;
    private readonly    Dictionary<string, TagType>             tagTypeByName;
    private readonly    ComponentType                           unresolvedType;
    private readonly    List<ComponentType>                     componentTypes;
    private readonly    HashSet<ScriptType>                     scriptTypes;
    private readonly    ArchetypeKey                            searchKey;
    private readonly    List<string>                            unresolvedTagList;
    private readonly    HashSet<string>                         unresolvedTagSet;
    private readonly    List<UnresolvedComponent>               unresolvedComponentList;
    private readonly    Dictionary<string, UnresolvedComponent> unresolvedComponentMap;
    private readonly    Dictionary<BytesHash, RawKey>           rawKeyCache;
    private             Utf8JsonParser                          parser;
    private             Bytes                                   buffer;
    private             RawComponent[]                          components;
    private             int                                     componentCount;
    
    
    internal ComponentReader() {
        buffer                  = new Bytes(128);
        components              = new RawComponent[1];
        componentReader         = new ObjectReader(EntityStoreBase.Static.TypeStore) { ErrorHandler = ObjectReader.NoThrow };
        var schema              = EntityStoreBase.Static.EntitySchema;
        unresolvedType          = schema.unresolvedType;
        schemaTypeByKey         = schema.schemaTypeByKey;
        scriptTypeByType        = schema.scriptTypeByType;
        tagTypeByName           = schema.tagTypeByName;
        componentTypes          = new List<ComponentType>();
        scriptTypes             = new HashSet<ScriptType>(); // Cannot use Script. User code may override Equals() or GetHashCode()
        searchKey               = new ArchetypeKey();
        unresolvedTagList       = new List<string>();
        unresolvedTagSet        = new HashSet<string>();
        unresolvedComponentList = new List<UnresolvedComponent>();
        unresolvedComponentMap  = new Dictionary<string, UnresolvedComponent>();
        rawKeyCache             = new Dictionary<BytesHash, RawKey>(BytesHash.Equality);
    }
    
    internal string Read(DataEntity dataEntity, Entity entity, EntityStoreBase store)
    {
        componentCount      = 0;
        var hasTags         = dataEntity.tags?.Count > 0;
        var hasComponents   = !dataEntity.components.IsNull();
        if (!hasComponents && !hasTags) {
            return null;
        }
        var error = ReadRaw(dataEntity, entity);
        if (error != null) {
            return error;
        }
        SetEntityArchetype(dataEntity, entity, store);
        return ReadComponents(entity);
    }
    
    private string ReadRaw (DataEntity dataEntity, Entity entity)
    {
        parser.InitParser(dataEntity.components);
        var ev = parser.NextEvent();
        switch (ev)
        {
            case JsonEvent.Error:
                var error = parser.error.GetMessage();
                return $"{error}. id: {entity.id}";
            case JsonEvent.ValueNull:
                break;
            case JsonEvent.ObjectStart:
                ev = ReadRawComponents();
                if (ev != JsonEvent.ObjectEnd) {
                    // could support also scalar types in future: string, number or boolean
                    return $"'components' element must be an object. was {ev}. id: {entity.id}, component: '{parser.key}'";
                }
                break;
            default:
                return $"expect 'components' == object or null. id: {entity.id}. was: {ev}";
        }
        return null;
    }
    
    private string ReadComponents(Entity entity)
    {
        unresolvedComponentList.Clear();
        scriptTypes.Clear();
        foreach (var script in entity.Scripts) {
            var scriptType = scriptTypeByType[script.GetType()];
            scriptTypes.Add(scriptType);
        }
        for (int n = 0; n < componentCount; n++)
        {
            var component = components[n];
            buffer.Clear();
            var json        = new JsonValue(parser.GetInputBytes(component.start - 1, component.end));
            var schemaType  = component.rawKey.schemaType;
            if (schemaType == unresolvedType) {
                unresolvedComponentList.Add(new UnresolvedComponent(component.rawKey.key, json));
                continue;
            }
            switch (schemaType.kind) {
                case SchemaTypeKind.Script:
                    // --- read script
                    var scriptType = (ScriptType)schemaType;
                    scriptTypes.Remove(scriptType);
                    scriptType.ReadScript(componentReader, json, entity);
                    break;
                case SchemaTypeKind.Component:
                    var componentType   = (ComponentType)schemaType;
                    var heap            = entity.archetype.heapMap[componentType.structIndex]; // no range or null check required
                    // --- read & change component
                    heap.Read(componentReader, entity.compIndex, json);
                    break;
            }
            if (componentReader.Error.ErrSet) {
                return $"'components[{component.rawKey.key}]' - {componentReader.Error.GetMessage()}";
            }
        }
        // --- remove missing scripts from entity
        foreach (var scriptType in scriptTypes) {
            EntityUtils.RemoveEntityScript(entity, scriptType);
        }
        // --- add unresolved components
        if (unresolvedComponentList.Count > 0 ) {
            AddUnresolvedComponents(entity);
        }
        return null;
    }
    
    private void AddUnresolvedComponents(Entity entity)
    {
        ref var unresolved          = ref entity.GetComponent<Unresolved>();
        var componentList           = unresolvedComponentList;
        var unresolvedComponents    = unresolved.components;
        if (unresolvedComponents == null) {
            unresolved.components = new UnresolvedComponent[componentList.Count];
            componentList.CopyTo(unresolved.components);
            return;
        }
        var map = unresolvedComponentMap;
        map.Clear();
        foreach (var component in unresolvedComponents) {
            map[component.key] = component;
        }
        foreach (var component in componentList) {
            map[component.key] = component;
        }
        if (unresolvedComponents.Length != map.Count) {
            unresolvedComponents = unresolved.components= new UnresolvedComponent[map.Count];
        }
        int n = 0;
        foreach (var pair in map) {
            unresolvedComponents[n++] = pair.Value;
        }
    }
    
    /// <summary>
    /// Ensures the given entity present / moved to an <see cref="Archetype"/> that contains all components 
    /// within the current JSON payload.
    /// </summary>
    private void SetEntityArchetype(DataEntity dataEntity, Entity entity, EntityStoreBase store)
    {
        searchKey.Clear();
        var hasComponentTypes   = GetComponentTypes(ref searchKey.componentTypes);
        var tags                = dataEntity.tags;
        var hasTags             = tags?.Count > 0;
        if (!hasComponentTypes && !hasTags) {
            return; // early out in absence of components and tags
        }
        unresolvedTagList.Clear();
        if (hasTags) {
            AddTags(tags, searchKey);
        }
        searchKey.CalculateHashCode();
        // --- use / create Archetype with present components to eliminate structural changes for every individual component Read()
        var newArchetype = FindArchetype(searchKey, store);
        
        var curArchetype = entity.archetype;
        if (curArchetype != newArchetype)
        {
            entity.refArchetype = newArchetype;
            if (curArchetype == store.defaultArchetype) {
                entity.refCompIndex = newArchetype.AddEntity(entity.id);
            } else {
                entity.refCompIndex = curArchetype.MoveEntityTo(entity.id, entity.compIndex, newArchetype);
            }
        }
        if (unresolvedTagList.Count > 0) {
            AddUnresolvedTags(entity);
        }
    }
    
    private void AddUnresolvedTags(Entity entity)
    {
        ref var unresolved = ref entity.GetComponent<Unresolved>();
        var tags    = unresolved.tags;
        var tagList = unresolvedTagList;
        if (tags == null) {
            tags = unresolved.tags = new string[tagList.Count];
            int n = 0;
            foreach (var tag in tagList) {
                tags[n++] = tag;
            }
            return;
        }
        var set = unresolvedTagSet;
        set.Clear();
        foreach (var tag in tags) {
            set.Add(tag);   
        }
        foreach (var tag in tagList) {
            set.Add(tag);   
        }
        if (tags.Length != set.Count) {
            tags = unresolved.tags = new string[set.Count];
        }
        int i = 0;
        foreach (var tag in set) {
            tags[i++] = tag;
        }
    }
    
    private bool GetComponentTypes(ref ComponentTypes componentTypes)
    {
        var hasComponentTypes   = false;
        var count               = componentCount;
        for (int n = 0; n < count; n++)
        {
            ref var component   = ref components[n];
            var schemaType      = component.rawKey.schemaType;
            if (schemaType == unresolvedType) {
                // case: unresolved component
                hasComponentTypes = true;
                componentTypes.SetBit(unresolvedType.structIndex);
                continue;
            }
            if (schemaType.kind == SchemaTypeKind.Component)
            {
                var componentType = (ComponentType)schemaType;
                hasComponentTypes = true;
                componentTypes.SetBit(componentType.structIndex);
            }                
        }
        return hasComponentTypes;
    }
    
    private Archetype FindArchetype(ArchetypeKey searchKey, EntityStoreBase store)
    {
        if (store.TryGetValue(searchKey, out var archetypeKey)) {
            return archetypeKey.archetype;
        }
        var config = store.GetArchetypeConfig();
        componentTypes.Clear();
        for (int n = 0; n < componentCount; n++) {
            ref var component = ref components[n];
            var schemaType = component.rawKey.schemaType;
            if (schemaType.kind == SchemaTypeKind.Component) {
                componentTypes.Add((ComponentType)schemaType);
            }
        }
        if (unresolvedTagList.Count > 0) {
            componentTypes.Add(unresolvedType);
        }
        var newArchetype = Archetype.CreateWithComponentTypes(config, componentTypes, searchKey.tags);
        store.AddArchetype(newArchetype);
        return newArchetype;
    }
    
    private JsonEvent ReadRawComponents()
    {
        var ev = parser.NextEvent();
        while (true) {
            switch (ev) {
                case JsonEvent.ObjectStart:
                    var rawKey  = ToRawKey(parser.key);
                    var start   = parser.Position;
                    parser.SkipTree();
                    if (componentCount == components.Length) {
                        ArrayUtils.Resize(ref components, 2 * componentCount);
                    }
                    components[componentCount++] = new RawComponent(rawKey, start, parser.Position);
                    ev = parser.NextEvent();
                    if (ev == JsonEvent.ObjectEnd) {
                        return JsonEvent.ObjectEnd;
                    }
                    break;
                case JsonEvent.ObjectEnd:
                    return JsonEvent.ObjectEnd;
                default:
                    return ev;
            }
        }
    }
    
    private RawKey ToRawKey(in Bytes keyBytes)
    {
        var keyHash = new BytesHash(keyBytes);
        if (rawKeyCache.TryGetValue(keyHash, out RawKey rawKey)) {
            return rawKey;
        }
        var key = keyBytes.AsString();
        if (schemaTypeByKey.TryGetValue(key, out var schemaType)) {
            rawKey  = new RawKey(key, schemaType);
        } else {
            rawKey  = new RawKey(key, unresolvedType);
        }
        var bytesCopy = new Bytes(keyBytes);    // must create copy - given key Bytes will be mutated
        rawKeyCache.Add(new BytesHash(bytesCopy), rawKey);
        return rawKey;
    }
    
    private void AddTags(List<string> tagList, ArchetypeKey archetypeKey)
    {
        foreach (var tag in tagList) {
            if (!tagTypeByName.TryGetValue(tag, out var tagType)) {
                archetypeKey.componentTypes.SetBit(unresolvedType.structIndex);
                unresolvedTagList.Add(tag);
                continue;
            }
            archetypeKey.tags.SetBit(tagType.tagIndex);
        }
    }
}

internal readonly struct RawKey
{
    internal  readonly  string      key;            // never null
    internal  readonly  SchemaType  schemaType;     // never null

    public    override  string      ToString() => $"{key} - {schemaType.type.Name}";
    
    internal RawKey(string key, SchemaType schemaType) {
        this.key        = key;
        this.schemaType = schemaType;
    }
}

internal readonly struct RawComponent
{
    internal  readonly  RawKey      rawKey;
    internal  readonly  int         start;
    internal  readonly  int         end;

    public    override  string      ToString() => rawKey.ToString();
    
    internal RawComponent(in RawKey rawKey, int start, int end) {
        this.rawKey = rawKey;
        this.start  = start;
        this.end    = end;
    }
}