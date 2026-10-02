using System.Collections.ObjectModel;

namespace BVELauncher.App.ViewModels;

public static class CollectionSynchronization
{
    public static void Update<T>(ObservableCollection<T> current, IReadOnlyList<T> desired) where T : class
    {
        // 同じ項目のコンテナーを維持し、検索や折り畳みで全テンプレートを再生成しない。
        var retained = desired.ToHashSet();
        for (int i = current.Count - 1; i >= 0; i--) if (!retained.Contains(current[i])) current.RemoveAt(i);
        for (int i = 0; i < desired.Count; i++)
        {
            if (i < current.Count && ReferenceEquals(current[i], desired[i])) continue;
            int existing = current.IndexOf(desired[i]);
            if (existing >= 0) current.Move(existing, i); else current.Insert(i, desired[i]);
        }
    }
}
