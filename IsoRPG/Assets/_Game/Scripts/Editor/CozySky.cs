using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Включает COZY — систему неба и погоды.
    ///
    /// До этого небо у нас было либо заливкой одного цвета, либо статическим
    /// материалом. Оба варианта плохи по одной причине: небо в игре видно
    /// треть кадра, а работает оно как декорация, приклеенная к горизонту.
    /// COZY даёт живое: время суток, движение солнца и луны, облака, туман,
    /// дождь — и, что важнее для картинки, ОН ЖЕ ведёт освещение сцены,
    /// поэтому свет и небо перестают спорить друг с другом.
    ///
    /// Работает это так: в сцену ставится «Cozy Weather Sphere», и дальше он
    /// сам подменяет материал неба, крутит направленный свет и настраивает
    /// туман каждый кадр.
    ///
    /// Отсюда главное правило работы с ним: <b>наши ручные настройки неба,
    /// рассеянного света и тумана с этого момента не действуют.</b> Ставить
    /// их рядом бессмысленно — COZY перезапишет их в первом же кадре, и
    /// человек будет крутить ползунки, которые ни на что не влияют.
    ///
    /// Своё солнце гасим, если COZY привёз собственное: два направленных
    /// света дают двойные тени, и это видно сразу.
    /// </summary>
    public static class CozySky
    {
        private const string Prefab =
            "Packages/com.distantlands.cozy.core/Content/Prefabs/Cozy Weather Sphere.prefab";

        private const string HolderName = "Cozy Weather Sphere";

        [MenuItem("Tools/IsoRPG/Небо: включить COZY", priority = 57)]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            var already = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                                .FirstOrDefault(g => g.name == HolderName);

            if (already != null) Object.DestroyImmediate(already);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);

            if (asset == null)
            {
                Debug.LogError("[IsoRPG] Не нашёл " + Prefab +
                               " — COZY не поставлен или лежит иначе.");
                return;
            }

            var sphere = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            sphere.name = HolderName;
            sphere.transform.position = Vector3.zero;

            // Своё солнце — только если COZY привёз собственное.
            //
            // Проверяем, а не предполагаем: у разных сборок пакета состав
            // префаба разный, и погасить единственный источник света значит
            // выключить сцену целиком. Это ровно та ошибка, которую я уже
            // сделал сегодня с рассеянным светом.
            var theirs = sphere.GetComponentsInChildren<Light>(true)
                               .Where(l => l.type == LightType.Directional)
                               .ToArray();

            int hushed = 0;

            if (theirs.Length > 0)
            {
                foreach (var light in Object.FindObjectsByType<Light>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (light.type != LightType.Directional) continue;
                    if (theirs.Contains(light)) continue;

                    light.enabled = false;
                    EditorUtility.SetDirty(light);
                    hushed++;
                }
            }

            // Материал неба ставим ЯВНО, не полагаясь на то, что COZY
            // подменит его сам.
            //
            // Он и подменит — но в первом кадре игры, а до того в настройках
            // сцены лежит то, что там лежало. У нас там лежит сломанное
            // небо-купол, и стартовый кадр вышел бы чёрным. Хуже: если COZY
            // по какой-то причине не запустится, чёрным останется всё.
            var theirSky = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.distantlands.cozy.core/Content/Art/Materials/Skybox.mat");

            if (theirSky != null)
            {
                RenderSettings.skybox = theirSky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            }
            else
            {
                Debug.LogWarning("[IsoRPG] Не нашёл материал неба COZY — " +
                                 "в настройках сцены осталось прежнее.");
            }

            var camera = Camera.main;

            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                EditorUtility.SetDirty(camera);
            }

            Selection.activeGameObject = sphere;

            Debug.Log("[IsoRPG] COZY включён. Его направленных источников: " + theirs.Length +
                      (hushed > 0 ? ", наших погашено " + hushed : ", наше солнце оставлено") +
                      ". Небо, туман и рассеянный свет теперь ведёт он — " +
                      "наши ручные настройки этих трёх вещей больше не действуют.");
        }

        [MenuItem("Tools/IsoRPG/Небо: выключить COZY", priority = 58)]
        public static void Remove()
        {
            var sphere = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                               .FirstOrDefault(g => g.name == HolderName);

            if (sphere != null) Object.DestroyImmediate(sphere);

            // Возвращаем своё солнце: без этого сцена останется без света
            // вовсе, и «выключить небо» будет означать «выключить всё».
            foreach (var light in Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;

                light.enabled = true;
                EditorUtility.SetDirty(light);
            }

            Debug.Log("[IsoRPG] COZY убран, своё солнце включено обратно.");
        }
    }
}
