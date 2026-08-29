using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Настраивает силуэты персонажей сквозь препятствия.
    ///
    /// Зачем. В изометрии камера привязана к углу, и обойти дерево, чтобы
    /// увидеть себя, игрок не может. Скрытый за кроной персонаж — это
    /// потерянное управление: не видно ни где ты, ни что с тобой происходит.
    ///
    /// Как. Отдельный проход рендера рисует персонажей поверх всего, но
    /// только там, где они закрыты геометрией. Проход добавляется в настройки
    /// рендера как Render Objects: он берёт заданный слой и рисует его своим
    /// материалом.
    ///
    /// Почему слоем, а не компонентом на персонаже: так проход настраивается
    /// один раз и работает для всего, что попадёт в этот слой, — включая
    /// монстров, NPC и всё, что мы добавим потом.
    /// </summary>
    public static class SilhouetteSetup
    {
        private const string LayerName = "Characters";
        private const string ShaderPath = "Assets/_Game/Art/Shaders/Silhouette.shader";
        private const string AllyMaterialPath = "Assets/_Game/Art/Materials/M_Silhouette_Ally.mat";
        private const string EnemyMaterialPath = "Assets/_Game/Art/Materials/M_Silhouette_Enemy.mat";
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";

        // Свой зелёный, чужой красный — тот же язык, что у полосок здоровья
        // и рамок цели. Игрок не должен учить второй словарь ради силуэтов.
        private static readonly Color AllyColor = new Color(0.42f, 0.88f, 0.58f, 0.85f);
        private static readonly Color EnemyColor = new Color(0.92f, 0.36f, 0.32f, 0.85f);

        [MenuItem("Tools/IsoRPG/Настроить силуэты", priority = 14)]
        public static void Setup()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play настройки не сохраняются.", "Понятно");
                return;
            }

            var ally = EnsureMaterial(AllyMaterialPath, AllyColor);
            var enemy = EnsureMaterial(EnemyMaterialPath, EnemyColor);
            if (ally == null || enemy == null) return;

            int removed = RemoveFeature();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Материал силуэта готов" +
                      (removed > 0 ? ", убран старый проход рендера (" + removed + ")" : "") +
                      ". Пересобери песочницу.");
        }

        /// <summary>Номер слоя силуэтов — им пользуется сборщик сцены.</summary>
        public static int SilhouetteLayer => LayerMask.NameToLayer(LayerName);

        // ------------------------------------------------------------------

        /// <summary>
        /// Заводит слой, если его ещё нет. Правим напрямую настройки проекта:
        /// программного способа добавить слой у Unity нет.
        /// </summary>
        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0)
            {
                Debug.LogError("[IsoRPG] Не удалось открыть настройки слоёв.");
                return -1;
            }

            var tagManager = new SerializedObject(asset[0]);
            var layers = tagManager.FindProperty("layers");

            // Слои 0-7 заняты Unity, свои начинаются с восьмого.
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);

                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = name;
                    tagManager.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    return i;
                }
            }

            Debug.LogError("[IsoRPG] Свободных слоёв не осталось.");
            return -1;
        }

        private static Material EnsureMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

            if (shader == null)
            {
                Debug.LogError("[IsoRPG] Не найден шейдер " + ShaderPath);
                return null;
            }

            var material = new Material(shader)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path)
            };
            material.SetColor("_BaseColor", color);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Убирает проход рендера, если он остался от прежнего подхода.
        ///
        /// Первая версия силуэтов делалась отдельным проходом Render Objects.
        /// Выглядело архитектурно правильнее: настроил один раз, работает для
        /// всего слоя. На деле проход перекрывает состояние глубины своим, и
        /// силуэт рисовался поверх всего, включая открытого персонажа.
        /// Оставлять его нельзя — он продолжит заливать модели цветом.
        /// </summary>
        private static int RemoveFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null) return 0;

            var stale = data.rendererFeatures
                .Where(f => f != null && f.name == "Silhouette")
                .ToList();

            foreach (var feature in stale)
            {
                data.rendererFeatures.Remove(feature);
                Object.DestroyImmediate(feature, true);
            }

            if (stale.Count > 0) EditorUtility.SetDirty(data);

            return stale.Count;
        }
    }
}
