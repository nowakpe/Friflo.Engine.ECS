﻿using Friflo.Fliox.Engine.Client;
using Friflo.Fliox.Engine.ECS;

namespace Friflo.Fliox.Editor;

[Component("TestComponent")]
public struct  TestComponent : IComponent
{
    public string name;
}

public struct TestTag : IEntityTag { }


[Script("TestScript")]
public class TestScript : Script
{
    public string name;
}

public static class TestBed
{
    internal static void AddSampleEntities(GameDataSync sync)
    {
        var store   = sync.Store;
        var root    = store.StoreRoot;
        root.AddComponent(new Position(1, 1, 1));
        root.AddComponent(new EntityName("root"));
        var child   = CreateEntity(store, 2);
        child.AddComponent(new Position(2, 2, 2));

        root.AddChild(child);
        root.AddChild(CreateEntity(store, 3));
        root.AddChild(CreateEntity(store, 4));
        root.AddChild(CreateEntity(store, 5));
        root.AddChild(CreateEntity(store, 6));
        root.AddChild(CreateEntity(store, 7));
        
        var many = store.CreateEntity();
        many.AddComponent(new EntityName("many-root"));
        AddManyEntities(many, new [] { 100, 100 }, 0);
        // AddManyEntities(many, new [] { 100, 100, 100 }, 0);
        
        root.AddChild(many);
    }
    
    private static void AddManyEntities(GameEntity entity, int[] counts, int depth)
    {
        if (depth >= counts.Length) {
            return;
        }
        var count   = counts[depth];
        var store   = entity.Store;
        for (int n = 0; n < count; n++) {
            var child = store.CreateEntity();
            entity.AddChild(child);
            AddManyEntities(child, counts, depth + 1);
        }
    }
    
    private static GameEntity CreateEntity(GameEntityStore store, int id)
    {
        var entity = store.CreateEntity();
        entity.AddComponent(new EntityName("child-" + id));
        return entity;
    }
}