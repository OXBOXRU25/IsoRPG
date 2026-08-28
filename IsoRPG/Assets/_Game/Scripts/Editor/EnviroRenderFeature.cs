using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Добавляет в наш URP-рендерер функцию отрисовки Enviro.
    ///
    /// Почему без неё система выглядит сломанной. В URP небо, облака и туман
    /// Enviro рисует не сам объект в сцене, а <c>ScriptableRendererFeature</c>,
    /// добавленная в ассет рендерера. Менеджер при этом честно работает:
    /// считает время, ведёт погоду, двигает солнце — но выводить ему нечем.
    /// На экране остаётся то, что было раньше, и это неотличимо от «Enviro не
    /// установлен».
    ///
    /// Я на этом потерял час: доказывал, что вижу объёмные облака, глядя на
    /// нарисованную панораму, которая просто осталась в RenderSettings.
    /// Настоящая проверка оказалась простой — снять старое небо и посмотреть,
    /// нарисует ли новая система хоть что-нибудь.
    ///
    /// Функция создаётся отражением: её тип лежит в сборке Enviro, и прямая
    /// ссылка привязала бы компиляцию всего проекта к присутствию набора.
    /// </summary>
    public static class EnviroRenderFeature
    {
        private const string FeatureType = "Enviro.EnviroURPRenderFeature";

        /// <summary>
        /// Объявляет символ ENVIRO_URP.
        ///
        /// Половина Enviro закрыта директивой <c>#if ENVIRO_URP</c>: отрисовка,
        /// туман, объёмные облака. Без символа эти файлы не компилируются
        /// вовсе — тип отрисовки не существует, и найти его нельзя никаким
        /// отражением. Обычно символ ставит сам Enviro, когда в его инспекторе
        /// выбирают конвейер; мы ставим руками, потому что инспектор нам не
        /// открыть.
        ///
        /// После правки Unity перекомпилирует проект, и только СЛЕДУЮЩИЙ
        /// прогон увидит новые типы. Одним заходом это не делается.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Небо Enviro: объявить ENVIRO_URP", priority = 23)]
        public static void AddDefine()
        {
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);

            if (defines.Split(new[] { Char.Parse(";") },
                              StringSplitOptions.RemoveEmptyEntries)
                       .Contains("ENVIRO_URP"))
            {
                Debug.Log("[IsoRPG] ENVIRO_URP уже объявлен.");
                return;
            }

            defines = string.IsNullOrEmpty(defines) ? "ENVIRO_URP" : defines + ";ENVIRO_URP";
            PlayerSettings.SetScriptingDefineSymbols(target, defines);

            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Объявлен ENVIRO_URP. Символы: " + defines +
                      ". Нужен ещё один прогон — типы появятся после перекомпиляции.");
        }

        /// <summary>
        /// Снимает символ ENVIRO_URP.
        ///
        /// Нужен, потому что символ включает код, который в Unity 6 не
        /// компилируется: URP 17 перешёл на RenderGraph и выбросил методы
        /// Configure, OnCameraSetup и Execute(ScriptableRenderContext, ref
        /// RenderingData), а Enviro 3.0.4 писан под URP 14–16 и переопределяет
        /// именно их. Пока символ объявлен, проект не собирается вовсе.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Небо Enviro: снять ENVIRO_URP", priority = 26)]
        public static void RemoveDefine()
        {
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);

            var list = defines.Split(new[] { Char.Parse(";") },
                                     StringSplitOptions.RemoveEmptyEntries)
                              .Where(d => d != "ENVIRO_URP")
                              .ToArray();

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] ENVIRO_URP снят. Символы: " + string.Join(";", list));
        }

        [MenuItem("Tools/IsoRPG/Небо Enviro: включить отрисовку в URP", priority = 24)]
        public static void Add()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                                .Select(a => a.GetType(FeatureType))
                                .FirstOrDefault(t => t != null);

            if (type == null)
            {
                Debug.LogWarning("[IsoRPG] Тип " + FeatureType + " не найден — Enviro не установлен?");
                return;
            }

            int added = 0, already = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Только наши рендереры: в проекте лежат ещё и чужие, из
                // демо-сцен наборов, и совать функцию в них незачем.
                if (!path.StartsWith("Assets/Settings/")) continue;

                var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (data == null) continue;

                if (data.rendererFeatures.Any(f => f != null && f.GetType() == type))
                {
                    already++;
                    continue;
                }

                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = type.Name;

                // Функция должна лежать ВНУТРИ ассета рендерера, а не рядом:
                // иначе она потеряется при первой же перезагрузке проекта, и
                // небо снова пропадёт — молча.
                AssetDatabase.AddObjectToAsset(feature, data);

                data.rendererFeatures.Add(feature);
                EditorUtility.SetDirty(data);
                added++;

                Debug.Log("[IsoRPG] Отрисовка Enviro добавлена в " + path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Рендереров обработано: добавлено " + added +
                      ", уже было " + already + ".");
        }

        [MenuItem("Tools/IsoRPG/Небо Enviro: выключить отрисовку в URP", priority = 25)]
        public static void RemoveFeature()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                                .Select(a => a.GetType(FeatureType))
                                .FirstOrDefault(t => t != null);

            if (type == null) return;

            int removed = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/Settings/")) continue;

                var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (data == null) continue;

                var found = data.rendererFeatures
                                .Where(f => f != null && f.GetType() == type)
                                .ToList();

                foreach (var f in found)
                {
                    data.rendererFeatures.Remove(f);
                    UnityEngine.Object.DestroyImmediate(f, true);
                    removed++;
                }

                if (found.Count > 0) EditorUtility.SetDirty(data);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[IsoRPG] Отрисовка Enviro убрана из рендереров: " + removed + ".");
        }
    }
}
