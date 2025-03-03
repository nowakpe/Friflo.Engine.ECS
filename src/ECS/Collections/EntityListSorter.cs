using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Friflo.Engine.ECS;

public enum SortOrder
{
    None,
    Ascending,
    Descending
}

internal static class TypeMember<TComponent, TField>
{
    private static readonly Dictionary<string, MemberGetter<TComponent,TField>> GetterMap = new();   
    
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Not called for NativeAOT")]
    internal static MemberGetter<TComponent,TField> Getter(string memberName)
    {
        if (GetterMap.TryGetValue(memberName, out var getter)) {
            return getter;
        }
        var arg         = Expression.Parameter(typeof(TComponent), "component"); // "component" parameter name in MemberGetter<,>
        var expr        = Expression.PropertyOrField(arg, memberName);
        var compiled    = Expression.Lambda<MemberGetter<TComponent, TField>>(expr, arg).Compile();
        GetterMap.Add(memberName, compiled);
        return compiled;
    }
}

internal delegate TField MemberGetter<in TComponent, out TField> (TComponent component);


public struct ComponentField<TField> where TField : IComparable<TField>
{
    public  int     entityId;
    public  byte    hasField;
    public  TField  field;

    public override string ToString() {
        if (hasField == 0) {
            return $"id: {entityId}, value: null";    
        }
        return $"id: {entityId}, value: {field}";
    }
    
#if !NET5_0_OR_GREATER
    private class GenericComparerAsc : IComparer<ComponentField<TField>>
    {
        public int Compare(ComponentField<TField> e1, ComponentField<TField> e2) {
            var hasValueDiff = e1.hasField - e2.hasField;
            return hasValueDiff != 0 ? hasValueDiff : Comparer<TField>.Default.Compare(e1.field, e2.field);
        }
    }
    
    private class GenericComparerDesc : IComparer<ComponentField<TField>>
    {
        public int Compare(ComponentField<TField> e1, ComponentField<TField> e2) {
            var hasValueDiff = e2.hasField - e1.hasField;
            return hasValueDiff != 0 ? hasValueDiff : Comparer<TField>.Default.Compare(e2.field, e1.field);
        }
    }

    private static readonly GenericComparerAsc  ComparerAsc  = new ();
    private static readonly GenericComparerDesc ComparerDesc = new ();
#endif

    private static readonly Comparison<ComponentField<TField>> ComparisonAsc = (e1, e2) => {
        var hasValueDiff = e1.hasField - e2.hasField;
        return hasValueDiff != 0 ? hasValueDiff : Comparer<TField>.Default.Compare(e1.field, e2.field);
    };
    
    private static readonly Comparison<ComponentField<TField>> ComparisonDesc = (e1, e2) => {
        var hasValueDiff = e2.hasField - e1.hasField;
        return hasValueDiff != 0 ? hasValueDiff : Comparer<TField>.Default.Compare(e2.field, e1.field);
    };

    
    internal static ComponentField<TField>[] Sort<TComponent>(EntityList  entities, string memberName, SortOrder sortOrder, ComponentField<TField>[] fields)
        where TComponent : struct, IComponent
    {
        var structIndex = StructInfo<TComponent>.Index;
        var count       = entities.Count;
        if (fields == null || fields.Length < count) {
            fields = new ComponentField<TField>[count];
        }
        var nodes   = entities.entityStore.nodes;
        var ids     = entities.ids;
        var getter  = TypeMember<TComponent, TField>.Getter(memberName);
        
        for (int index = 0; index < count; index++)
        {
            var id          = ids[index];
            ref var node    = ref nodes[id];
            var heap        = node.archetype?.heapMap[structIndex];
            ref var entry   = ref fields[index];
            entry.entityId  = id;
            if (heap == null) {
                entry.hasField = 0;
                continue;
            }
            ref var component = ref ((StructHeap<TComponent>)heap).components[node.compIndex];
            entry.field     = getter(component);
            entry.hasField  = 1;
        }

        switch (sortOrder) {
            case SortOrder.None:
                return fields;
            case SortOrder.Ascending:
                Span<ComponentField<TField>> span = new Span<ComponentField<TField>>(fields, 0, count);
#if NET5_0_OR_GREATER
                span.Sort(ComparisonAsc);
#else
                Array.Sort(fields, 0, count, ComparerAsc);  // allocates a single System.Comparision<ComponentField<>> instance
#endif
                break;
            case SortOrder.Descending:
                span = new Span<ComponentField<TField>>(fields, 0, count);
#if NET5_0_OR_GREATER
                span.Sort(ComparisonDesc);
#else
                Array.Sort(fields, 0, count, ComparerDesc);  // allocates a single System.Comparision<ComponentField<>> instance
#endif
                break;
        }
        for (int n = 0; n < count; n++) {
            ids[n] = fields[n].entityId;
        }
        return fields;
    }
}
