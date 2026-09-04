using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Кладёт эффект свечения добычи туда, где его найдёт игра.
    ///
    /// Решение Павла 05.09.2026: вместо коробки над трупом — свечение на самом
    /// теле. Эффект берём готовый из набора Synty: там 180 штук, и подходящий
    /// уже есть.
    ///
    /// Копия, а не ссылка: игра ищет эффект в `Resources`, потому что труп
    /// создаётся в ходе боя и заранее ссылку в него не положить. Оригинал в
    /// наборе остаётся нетронутым.
    /// </summary>
    public static class LootGlowKit
    {
        private const string Source = "Assets/Synty/PolygonParticleFX/Prefabs/FX_GlowSpot_02.prefab";
        private const string Folder = "Assets/_Game/Resources/FX";
        private const string Target = Folder + "/LootGlow.prefab";

        public static void Apply()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);

            if (source == null)
            {
                Debug.LogError("[IsoRPG] Нет эффекта свечения: " + Source);
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Game/Resources", "FX");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(Target) != null)
            {
                Debug.Log("[IsoRPG] Свечение добычи уже на месте: " + Target);
                return;
            }

            if (!AssetDatabase.CopyAsset(Source, Target))
            {
                Debug.LogError("[IsoRPG] Не удалось скопировать свечение в " + Target);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Свечение добычи положено в " + Target);
        }
    }
}
