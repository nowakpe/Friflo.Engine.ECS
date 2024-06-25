﻿using Friflo.Engine.ECS;
using static NUnit.Framework.Assert;

// ReSharper disable InconsistentNaming
namespace Tests.ECS.Index {


public static partial class Test_Index
{
    private static void QueryArg1 (IndexContext cx)
    {
        var store = cx.store;
        var query1  = store.Query<Position>().                  HasValue<IndexedName,   string>("find-me");
        var query2  = store.Query<Position>().                  HasValue<IndexedInt,    int>   (123);
        var query3  = store.Query<Position>().                  HasValue<IndexedName,   string>("find-me").
                                                                HasValue<IndexedInt,    int>   (123);
        var query4  = store.Query<Position>().                  HasValue<AttackComponent, Entity>(cx.target);
        cx.query1 = query1;
        cx.query2 = query2;
        cx.query3 = query3;
        cx.query4 = query4;
        {
            int count = 0;
            query1.ForEachEntity((ref Position pos, Entity entity) => {
                switch (count++) {
                    case 0: AreEqual(11, entity.Id); AreEqual(11f, pos.x); break;
                    case 1: AreEqual(13, entity.Id); AreEqual(13f, pos.x); break;
                }
                AreEqual("find-me", entity.GetComponent<IndexedName>().name);
            });
            AreEqual(2, count);
        } { 
            int count = 0;
            query2.ForEachEntity((ref Position pos, Entity entity) => {
                switch (count++) {
                    case 0: AreEqual(12, entity.Id); AreEqual(12f, pos.x); break;
                    case 1: AreEqual(13, entity.Id); AreEqual(13f, pos.x); break;
                }
                AreEqual(123, entity.GetComponent<IndexedInt>().value);
            });
            AreEqual(2, count);
        } { 
            var count = 0;
            query3.ForEachEntity((ref Position pos, Entity entity) => {
                switch (count++) {
                    case 0: AreEqual(11, entity.Id); AreEqual(11f, pos.x); break;
                    case 1: AreEqual(13, entity.Id); AreEqual(13f, pos.x); break;
                    case 2: AreEqual(12, entity.Id); AreEqual(12f, pos.x); break;
                }
            });
            AreEqual(3, count);
        } {
            var count = 0;
            query4.ForEachEntity((ref Position pos, Entity entity) => {
                count++;
                AreEqual(13,        entity.Id);     AreEqual(13f, pos.x);
                AreEqual(cx.target, entity.GetComponent<AttackComponent>().target);
            });
            AreEqual(1, count);
        }
    }
}

}
