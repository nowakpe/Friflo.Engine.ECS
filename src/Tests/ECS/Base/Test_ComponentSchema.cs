using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Tests.ECS.Index;
using Tests.ECS.Relations;
using Tests.Examples;
using static NUnit.Framework.Assert;

// ReSharper disable InconsistentNaming
namespace Tests.ECS.Base {

public static class Test_ComponentSchema
{
    [Test]
    public static void Test_EntityTags() {
        var schema      = EntityStore.GetEntitySchema();
        AreEqual(23,     schema.Tags.Length);
        
        var tags = schema.Tags;
        IsNull(tags[0]);
        for (int n = 1; n < tags.Length; n++) {
            var type = tags[n];
            AreEqual(n,                 type.TagIndex);
            AreEqual(SchemaTypeKind.Tag, type.Kind);
            IsNull  (type.ComponentKey);
        }
        AreEqual(22,                     schema.TagTypeByType.Count);
        AreEqual(21,                     schema.TagTypeByName.Count);
        {
            var testTagType = schema.TagTypeByType[typeof(TestTag)];
            AreEqual(typeof(TestTag),       testTagType.Type);
            AreEqual("tag: [#TestTag]",     testTagType.ToString());
        } {
            var testTagType = schema.GetTagType<TestTag>();
            AreEqual(typeof(TestTag),       testTagType.Type);
            AreEqual("tag: [#TestTag]",     testTagType.ToString());
        } {
            var testTagType = schema.TagTypeByName["test-tag"];
            AreEqual(typeof(TestTag),       testTagType.Type);
            AreEqual("tag: [#TestTag]",     testTagType.ToString());
        } {
            var testTagType = schema.TagTypeByName[nameof(TestTag3)];
            AreEqual(typeof(TestTag3),      testTagType.Type);
            AreEqual("tag: [#TestTag3]",    testTagType.ToString());
        }
    }
    
    [Test]
    public static void Test_ComponentTypes()
    {
        var schema      = EntityStore.GetEntitySchema();
        var components  = schema.Components;
        var scripts     = schema.Scripts;
        
        AreEqual("components: 79  scripts: 10  entity tags: 22", schema.ToString());
        AreEqual(80,    components.Length);
        AreEqual(11,    scripts.Length);
        
        AreEqual(85,    schema.SchemaTypeByKey.Count);
        AreEqual(79,    schema.ComponentTypeByType.Count);
        AreEqual(72,    schema.ComponentTypes.Count);
        AreEqual( 7,    schema.RelationTypes.Count);
        AreEqual(10,    schema.ScriptTypeByType.Count);
        
        IsNull(components[0]);
        for (int n = 1; n < components.Length; n++) {
            var type = components[n];
            AreEqual(n, type.StructIndex);
            AreEqual(SchemaTypeKind.Component, type.Kind);
            if (type.Type == typeof(NonSerializedComponent) ||
                type.Type == typeof(TreeNode)) {
                IsNull (type.ComponentKey);
            } else {
                NotNull(type.ComponentKey);    
            }
        }
        {
            var schemaType = schema.SchemaTypeByKey["pos"];
            AreEqual(typeof(Position), schemaType.Type);
        } {
            var schemaType = schema.SchemaTypeByKey["test"];
            AreEqual(typeof(TestComponent), schemaType.Type);
        } {
            var componentType = schema.GetComponentType<MyComponent1>();
            AreEqual("my1",                             componentType.ComponentKey);
            AreEqual("Component: [MyComponent1]",       componentType.ToString());
            AreEqual(4,                                 componentType.StructSize);
            IsTrue  (                                   componentType.IsBlittable);
        } {
            var componentType = schema.GetRelationType<IntRelation>();
            AreEqual("Relation: [IntRelation]",        componentType.ToString());
        }
        // --- Engine.ECS types
        AssertBlittableComponent<Position>      (schema, true);
        AssertBlittableComponent<Rotation>      (schema, true);
        AssertBlittableComponent<Scale3>        (schema, true);
        AssertBlittableComponent<Transform>     (schema, true);
        AssertBlittableComponent<EntityName>    (schema, true);
        AssertBlittableComponent<Unresolved>    (schema, false);
        
        // --- BCL types
        AssertBlittableComponent<BlittableEnum>         (schema, true);
        AssertBlittableComponent<BlittableDatetime>     (schema, true);
        AssertBlittableComponent<BlittableGuid>         (schema, true);
        AssertBlittableComponent<BlittableBigInteger>   (schema, true);
        AssertBlittableComponent<BlittableUri>          (schema, true);
        AssertBlittableComponent<BlittableTypes>        (schema, true);
        
        
        // --- Test blittable types
        AssertBlittableComponent<MyComponent1>  (schema, true);
        AssertBlittableComponent<MyComponent1>  (schema, true);
        AssertBlittableComponent<ByteComponent> (schema, true);
        
        // --- Test non-blittable types
        AssertBlittableComponent<NonBlittableArray>     (schema, false);
        AssertBlittableComponent<NonBlittableList>      (schema, false);
        AssertBlittableComponent<NonBlittableDictionary>(schema, false);
        AssertBlittableComponent<NonBlittableCycle>     (schema, false);
        AssertBlittableComponent<NonBlittableCycle2>    (schema, false);
        AssertBlittableComponent<NonBlittableComponent> (schema, false);
        AssertBlittableComponent<NonBlittableClass>     (schema, false);
    }
    
    [Test]
    public static void Test_ComponentTypes_CopyComponent()
    {
        var c1 = EntityUtils.CopyComponent(new MyComponent1{ a = 55 }, default, default); // blittable component
        AreEqual(55, c1.a);
        
        var list1 = new List<int> { 20 };
        var c2 = EntityUtils.CopyComponent(new CopyComponent{ list = list1 }, default, default); // non blittable component
        list1[0] = 30;              // changing the list has no effect on the copied component.
        AreEqual(20, c2.list[0]);   // still 20
    }
    
    [Test]
    public static void Test_Component_symbols()
    {
        var schema      = EntityStore.GetEntitySchema();
        
        var entityName = schema.GetComponentType<EntityName>();
        AreEqual("N",                           entityName.SymbolName);
        AreEqual(new SymbolColor(0,0,0),        entityName.SymbolColor);
        
        var disabled = schema.GetTagType<Disabled>();
        AreEqual("D",                           disabled.SymbolName);
        AreEqual(new SymbolColor(150,150,150),  disabled.SymbolColor);
        
        var unique = schema.GetComponentType<UniqueEntity>();
        AreEqual("UQ",                          unique.SymbolName);
        AreEqual(new SymbolColor(255,145,0),    unique.SymbolColor);
        
        var unresolved = schema.GetComponentType<Unresolved>();
        AreEqual("Un",                          unresolved.SymbolName);
        AreEqual(new SymbolColor(255,0,0),      unresolved.SymbolColor);
        var col = unresolved.SymbolColor.Value;
        AreEqual(255,   col.r);
        AreEqual(0,     col.g);
        AreEqual(0,     col.b);
        
        var myComponent1 = schema.GetComponentType<MyComponent1>();
        AreEqual("M1",  myComponent1.SymbolName);
        AreEqual(null,  myComponent1.SymbolColor);
        
        var myComponent2 = schema.GetComponentType<MyComponent2>();
        AreEqual("M2",  myComponent2.SymbolName);
        AreEqual(null,  myComponent2.SymbolColor);
        
        var myComponent3 = schema.GetComponentType<MyComponent3>();
        AreEqual("M3",  myComponent3.SymbolName);
        AreEqual(null,  myComponent3.SymbolColor);
        
        var myComponent4 = schema.GetComponentType<MyComponent4>();
        AreEqual("M",   myComponent4.SymbolName);
        AreEqual(null,  myComponent4.SymbolColor);
        
        var myComponent5 = schema.GetComponentType<MyComponent5>();
        AreEqual("M",   myComponent5.SymbolName);
        AreEqual(null,  myComponent5.SymbolColor);
        
        _ = new ComponentSymbolAttribute("abc"); // cover constructor
    }
    
    private static void AssertBlittableComponent<T>(EntitySchema schema, bool expect) where T : struct, IComponent {
        var componentType = schema.ComponentTypeByType[typeof(T)];
        AreEqual(expect, componentType.IsBlittable);
    }
    
    private static void AssertBlittableScript<T>(EntitySchema schema, bool expect)  where T : Script {
        var scriptType = schema.ScriptTypeByType[typeof(T)];
        AreEqual(expect, scriptType.IsBlittable);
    }
    
    [Test]
    public static void Test_ScriptTypes()
    {
        var schema      = EntityStore.GetEntitySchema();
        var scripts     = schema.Scripts;
        IsNull(scripts[0]);
        for (int n = 1; n < scripts.Length; n++) {
            var type = scripts[n];
            AreEqual(n, type.ScriptIndex);
            AreEqual(SchemaTypeKind.Script, type.Kind);
            NotNull (type.ComponentKey);
        }
        
        var scriptType = schema.GetScriptType<TestComponent>();
        AreEqual("test",                        scriptType.ComponentKey);
        AreEqual("Script: [*TestComponent]",    scriptType.ToString());
        
        AreEqual(typeof(Position),  schema.SchemaTypeByKey["pos"].Type);
        AreEqual("test",            schema.ScriptTypeByType[typeof(TestComponent)].ComponentKey);
        
        AssertBlittableScript<TestComponent>(schema, true);
    }
    
    [Test]
    public static void Test_ComponentTypes_Sorting()
    {
        var schema      = EntityStore.GetEntitySchema();
        var components = schema.ComponentTypeByType.Values.ToArray();
        Array.Sort(components);
        IsTrue(components[0].Type   == typeof(AttackComponent));
        IsTrue(components[^1].Type  == typeof(Velocity));
    }
    
    [Test]
    public static void Test_Tags_Sorting()
    {
        var schema  = EntityStore.GetEntitySchema();
        var tags    = schema.TagTypeByType.Values.ToArray();
        Array.Sort(tags);
        IsTrue(tags[0].Type   == typeof(ExampleECS.Cat));
        IsTrue(tags[^1].Type  == typeof(TestTag9));
    }
}

}

