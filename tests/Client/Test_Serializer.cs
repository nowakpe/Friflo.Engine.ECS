﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Friflo.Fliox.Engine.Client;
using Friflo.Fliox.Engine.ECS;
using NUnit.Framework;
using Tests.ECS;
using Tests.Utils;
using static NUnit.Framework.Assert;

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable HeuristicUnreachableCode
// ReSharper disable InconsistentNaming
namespace Tests.Client;

public static class Test_Serializer
{
    [Test]
    public static async Task Test_Serializer_write_scene()
    {
        var store       = new GameEntityStore(PidType.UsePidAsId);
        var serializer  = new GameDataSerializer(store);

        var entity  = store.CreateEntity(10);
        entity.AddComponent(new Position { x = 1, y = 2, z = 3 });
        entity.AddBehavior(new TestBehavior1 { val1 = 10 });
        entity.AddTag<TestTag>();
        
        var child   = store.CreateEntity(11);
        entity.AddChild(child);
        AreEqual(2, store.EntityCount);
        
        // --- store game entities as scene sync
        {
            var fileName    = TestUtils.GetBasePath() + "assets/test_scene.json";
            var file        = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            serializer.WriteScene(file);
            file.Close();
        }
        // --- store game entities as scene async
        {
            var fileName    = TestUtils.GetBasePath() + "assets/test_scene_async.json";
            var file        = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            await serializer.WriteSceneAsync(file);
            file.Close();
        }
    }
    
    [Test]
    public static void Test_Serializer_empty_scene()
    {
        var store       = new GameEntityStore(PidType.UsePidAsId);
        var serializer  = new GameDataSerializer(store);

        var stream = new MemoryStream();
        serializer.WriteScene(stream);
        var str = MemoryStreamAsString(stream);
        stream.Close();
        AreEqual("[]", str);
        
        AreEqual(0, store.EntityCount);
    }
    
    [Test]
    public static void Test_Serializer_write_scene_Perf()
    {
        var store       = new GameEntityStore(PidType.UsePidAsId);
        var serializer  = new GameDataSerializer(store);

        int count = 10;  // 1_000_000 ~ 1.227 ms
        for (int n = 0; n < count; n++) {
        var entity  = store.CreateEntity();
            entity.AddComponent(new Position { x = 1, y = 2, z = 3 });
            entity.AddTag<TestTag>();
        }
        
        AreEqual(count, store.EntityCount);
        
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var stream      = new MemoryStream();
        serializer.WriteScene(stream);
        var sizeKb = stream.Length / 1024;
        stream.Close();
        Console.WriteLine($"Write scene: entities: {count}, size: {sizeKb} kb, duration: {stopwatch.ElapsedMilliseconds} ms");
    }
    
    private static string MemoryStreamAsString(MemoryStream stream) {
        stream.Flush();
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }
}