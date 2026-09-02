using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Переставляет НПС на точку, указанную Павлоном по кадру из игры.
    ///
    /// Отдельным заданием, а не правкой `NpcPack`: тот собирает НПС с нуля и
    /// снёс бы всё, что на нём уже висит — выдачу квеста, голос, живые стойки,
    /// жесты. Двигать надо готового.
    ///
    /// Точка взята не на глаз: координаты героя лежат в сохранении
    /// (`character.json`), и они же видны на экране в углу — 53, −30. Поворот
    /// же в сохранении не хранится, поэтому берём ракурс камеры: на кадре
    /// герой стоит к ней спиной, значит смотрит он туда же, куда смотрит
    /// камера — это стартовый угол арены.
    /// </summary>
    public static class NpcMove
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        /// <summary>Точка героя из сохранения на момент кадра.</summary>
        private static readonly Vector2 Spot = new Vector2(53.32f, -30.05f);

        // Куда лицом — считаем по самому котелку, а не держим числом.
        //
        // Первый заход поставил 140°: я взял ракурс камеры, рассудив, что на
        // кадре герой стоит к ней спиной. Павлон посмотрел в игре: «ты его в
        // другую сторону развернул». Так и вышло — моё чтение кадра было
        // гипотезой, а он назвал ориентир прямо: лицом к котелку. Считать по
        // ориентиру надёжнее в любом случае: подвинут лагерь — угол уедет
        // сам, и дозорный не останется смотреть в пустоту.

        [MenuItem("Tools/IsoRPG/НПС: переставить на точку героя", priority = 44)]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            int moved = 0;

            foreach (var giver in Object.FindObjectsByType<IsoRPG.Quests.QuestGiver>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Высоту берём у земли, а потом уточняем по навигационной
                // сетке: она лежит выше грунта, и поставленный по грунту НПС
                // проваливался бы в него по щиколотку.
                float y = terrain != null
                    ? terrain.SampleHeight(new Vector3(Spot.x, 0f, Spot.y)) + terrain.transform.position.y
                    : giver.transform.position.y;

                if (NavMesh.SamplePosition(new Vector3(Spot.x, y, Spot.y), out var hit, 6f, NavMesh.AllAreas))
                    y = hit.position.y;

                var was = giver.transform.position;

                giver.transform.position = new Vector3(Spot.x, y, Spot.y);
                float facing = NpcPack.FaceHearth(giver.transform.position);
                giver.transform.rotation = Quaternion.Euler(0f, facing, 0f);

                EditorUtility.SetDirty(giver.transform);

                Debug.Log($"[IsoRPG] «{giver.name}»: {was.ToString("0.00")} → " +
                          $"{giver.transform.position.ToString("0.00")}, лицом на {facing:0}°.");

                moved++;
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[IsoRPG] НПС переставлено: {moved}.");
        }
    }
}
