using System;
using System.Diagnostics;
using Friflo.Engine.ECS;
using NUnit.Framework;
using static NUnit.Framework.Assert;


// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Tests.ECS {

public static class Test_EntityState
{
    [Test]
    public static void Test_EntityData_access()
    {
        var store   = new EntityStore();
        var entity  = store.CreateEntity(new Position(1,2,3), new EntityName("test"), Tags.Get<TestTag, TestTag2>());
        
        var data = entity.Data;
        IsFalse(data.IsNull);
        
        var tags = data.Tags;
        IsTrue(tags.Has<TestTag>());
        IsTrue(tags.Has<TestTag2>());
        
        AreSame (entity.Archetype,      data.Archetype);
        AreEqual(new Position(1,2,3),   data.Get<Position>());
        IsTrue  (                       data.Has<Position>());
        IsTrue  (                       data.TryGet<Position>(out var pos));
        AreEqual(new Position(1,2,3),   pos);
        
        AreEqual("test",                data.Get<EntityName>().value);
        
        IsFalse(                        data.Has<Scale3>());
        IsFalse(                        data.TryGet<Scale3>(out _));
        
        entity.DeleteEntity();
        
        data = entity.Data;
        IsTrue(data.IsNull);
        Throws<NullReferenceException>(() => {
            GetTags(entity);
        });
        Throws<NullReferenceException>(() => {
            GetComponent(entity);
        });
    }

    
    private static void GetTags(Entity entity) {
        var state = entity.Data;
        _ = state.Tags;
    }
    
    private static void GetComponent(Entity entity) {
        var data = entity.Data;
        _ = data.Get<Position>();
    }
    
    [Test]
    public static void Test_EntityData_access_Perf()
    {
        int count   = 100; // 1_000_000_000
        // Test_EntityData_access_Perf count: 1000000000, duration: 3286 ms
        var store   = new EntityStore();
        var entity  = store.CreateEntity(new Position(1,2,3), new EntityName("test"), new Scale3(), new MyComponent1());
        
        var data = entity.Data;
        
        var sw = new Stopwatch();
        sw.Start();
        for (int n = 0; n < count; n++) {
            data.Get<Position>();
            data.Get<EntityName>();
            data.Get<Scale3>();
            data.Get<MyComponent1>();
        }
        Console.WriteLine($"Test_EntityData_access_Perf count: {count}, duration: {sw.ElapsedMilliseconds} ms");
    }
}

}
