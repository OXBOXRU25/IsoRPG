using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Снимает подкраску с материалов луга.
    ///
    /// У шейдера растительности Synty пять цветовых параметров поверх
    /// текстуры: базовый цвет листвы, два шумовых, и та же пара для ствола.
    /// Они нужны, чтобы одним атласом красить разные биомы — осенний лес,
    /// хвойный, альпийский.
    ///
    /// Мы подменили ядро набора на URP-версию, и материалы луга поехали в
    /// красное с фиолетовым: их значения подкраски писались под прежний
    /// вариант шейдера, а тот считал иначе. Атлас при этом целый, силуэты
    /// правильные — красит именно подкраска.
    ///
    /// Обнуляем её. Цвет тогда берётся прямо из атласа, то есть таким, каким
    /// его нарисовал художник. Это не потеря: биомных перекрасок у нас всё
    /// равно нет, а вернуть их можно в любой момент — значения записаны в
    /// самом шейдере.
    /// </summary>
    public static class MeadowTint
    {
        private static readonly string[] Tints =
        {
            "_LeafBaseColour", "_LeafNoiseColour", "_LeafNoiseLargeColour",
            "_TrunkBaseColour", "_TrunkNoiseColour",
            "_GustHighlight",
        };

        [MenuItem("Tools/IsoRPG/Луг: снять подкраску", priority = 7)]
        public static void Run()
        {
            int touched = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Material",
                                                          new[] { "Assets/PolygonNatureBiomes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null) continue;

                bool changed = false;

                foreach (var name in Tints)
                {
                    if (!material.HasProperty(name)) continue;

                    // В ноль, а не в белый: у этих шейдеров подкраска
                    // ПРИБАВЛЯЕТСЯ к текстуре. Белый дал бы засветку в
                    // молоко, чёрный — чистый атлас.
                    material.SetColor(name, new Color(0f, 0f, 0f, 0f));
                    changed = true;
                }

                // Переключатели плоского цвета гасим: включённые, они рисуют
                // цветом вместо текстуры, и никакой атлас уже не поможет.
                foreach (var flat in new[] { "_LeafFlatColourSwitch", "_TrunkFlatColourSwitch" })
                {
                    if (!material.HasProperty(flat)) continue;

                    material.SetFloat(flat, 0f);
                    changed = true;
                }

                if (!changed) continue;

                EditorUtility.SetDirty(material);
                touched++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Подкраска снята у " + touched + " материалов луга.");
        }
    }
}
