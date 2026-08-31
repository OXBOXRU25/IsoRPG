using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Контроллер лошади — теперь от Malbers «Horse AnimSet Pro», а не от
    /// старого набора POLYGON Horse 2018 года (у того нашёлся всего один
    /// безымянный клип «Take 001», и включить его удалось только сняв
    /// галочку «Import Animation» — см. историю в PendingTasks/«horse»).
    ///
    /// Лошадь у нас декоративная и стоит на месте, поэтому вместо ходьбы —
    /// зацикленное «щипание травы» (H_Eat): ровно то, что заказано, а не
    /// стойка истуканом.
    /// </summary>
    public static class HorseAnimations
    {
        private const string Clips =
            "Assets/Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse";
        private const string Target = "Assets/_Game/Art/Animations/Controllers/AC_Horse.controller";

        [MenuItem("Tools/IsoRPG/Лошадь: собрать контроллер", priority = 40)]
        public static AnimatorController Build()
        {
            var eat = Clip("H_Eat.FBX");
            var idle = Clip("H_Idle_01.FBX") ?? eat;

            if (eat == null && idle == null)
            {
                Debug.LogError("[IsoRPG] Клипы лошади не нашлись — контроллер не собран.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Target) != null)
                AssetDatabase.DeleteAsset(Target);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(Target);
            var machine = controller.layers[0].stateMachine;

            var graze = machine.AddState("Graze");
            graze.motion = eat != null ? eat : idle;
            machine.defaultState = graze;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Контроллер лошади собран: щиплет траву («" +
                      (eat != null ? eat.name : idle.name) + "»), зациклено.");

            return controller;
        }

        private static AnimationClip Clip(string file)
        {
            string path = Clips + "/" + file;

            // Зацикливание клипа — настройка ИМПОРТА («Loop Time»), а не
            // поле AnimationClip.wrapMode: то читает только старый Legacy-
            // плеер, Mecanim его игнорирует. Ставим через ModelImporter,
            // иначе лошадь дожуёт один цикл и замрёт на последнем кадре.
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer != null)
            {
                var clips = importer.clipAnimations.Length > 0
                    ? importer.clipAnimations : importer.defaultClipAnimations;

                if (clips != null && clips.Length > 0 && !clips[0].loopTime)
                {
                    clips[0].loopTime = true;
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }

            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null) Debug.LogWarning("[IsoRPG] Клип лошади не найден: " + path);

            return clip;
        }
    }
}
