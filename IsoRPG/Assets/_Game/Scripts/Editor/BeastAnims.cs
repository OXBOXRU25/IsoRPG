using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using IsoRPG.Combat;
using IsoRPG.Player;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Раздаёт зверям полные наборы анимаций — не пересобирая их самих.
    ///
    /// Пересборка стаи (`boars`, `wolves`) ставит зверей заново по своим
    /// точкам и возвращает им настройки по умолчанию — в том числе
    /// уступание дороги, которое мы сняли заданием `no-avoidance`. Здесь
    /// нужно другое: те же звери на своих местах, но с новыми контроллерами.
    ///
    /// Заодно проставляем то, чего из контроллера не видно в игре: сколько
    /// у зверя ударов в серии и сколько равнозначных падений. Раньше бой слал
    /// всем номер от 1 до 6 — у кабана с четырьмя ударами номера 5 и 6 не
    /// совпадали ни с одним переходом, и каждый третий удар шёл без анимации.
    /// </summary>
    public static class BeastAnims
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        /// <summary>Что кому положено: имя существа — контроллер и числа.</summary>
        private struct Kind
        {
            public string Prefix;
            public string Controller;
            public int Attacks;
            public int Deaths;
            public int HowlKind;    // 0 — голоса у занятия нет
            public int[] Rests;     // какие занятия умеет: состояния Rest_N
        }

        private static readonly Kind[] Kinds =
        {
            new Kind
            {
                Prefix = "Волк",
                Controller = "Assets/_Game/Art/Animations/Controllers/AC_Wolf.controller",
                Attacks = WolfAnimations.AttackVariants,
                Deaths = 1,
                HowlKind = WolfAnimations.HowlKind,
                Rests = new[] { 1, 2, 3, WolfAnimations.HowlKind },
            },
            new Kind
            {
                Prefix = "Кабан",
                Controller = "Assets/_Game/Art/Animations/Controllers/AC_Boar.controller",
                Attacks = BoarAnimations.AttackVariants,
                Deaths = BoarAnimations.DeathVariants,

                // 1 ест, 2 сидит, 3 спит, 4 пьёт — все четыре есть в наборе.
                Rests = new[] { 1, 2, 3, 4 },
            },

            // Босс и гриб свои наборы получили ещё вчера, но числа им никто не
            // проставил: бой слал им те же 1..6. У босса семь ударов — седьмой
            // не играл никогда; у гриба три — номера 4, 5 и 6 уходили в пустоту,
            // то есть половина его ударов шла без анимации.
            new Kind
            {
                Prefix = "Босс",
                Controller = "Assets/_Game/Art/Animations/Controllers/AC_BoarBoss.controller",
                Attacks = BoarBossAnimations.AttackVariants,
                Deaths = 1,
                Rests = new[] { 1, 2, 3 },
            },
            new Kind
            {
                Prefix = "Вожак",
                Controller = "Assets/_Game/Art/Animations/Controllers/AC_BoarBoss.controller",
                Attacks = BoarBossAnimations.AttackVariants,
                Deaths = 1,
                Rests = new[] { 1, 2, 3 },
            },
            new Kind
            {
                Prefix = "Гриб",
                Controller = "Assets/_Game/Art/Animations/Controllers/AC_Mushroom.controller",
                Attacks = MushroomAnimations.AttackVariants,

                // Смерть у гриба выбирается НЕ жребием: вариант 2 — это
                // расплющивание тяжёлым оружием. Жребий тут врал бы.
                Deaths = 0,
            },
        };

        [MenuItem("Tools/IsoRPG/Существа: раздать полные анимации", priority = 40)]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            // Задание обязано САМО открывать нужную сцену: пакетный запуск
            // оставляет открытой ту, что была, и отчёт получается честным, но
            // про чужую сцену (грабли 01.09.2026, задание creature-layer).
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            // Контроллеры собираем ЗДЕСЬ же, а не отдельным заданием: иначе
            // раздача разойдётся со сборкой, и звери получат вчерашний набор.
            BoarAnimations.Build();
            WolfAnimations.Build();

            // Босса пересобираем тоже: его занятия звались «Eat/Sit/Sleep», а
            // общее правило теперь — `Rest_N`. Имя решает, узнает ли праздное
            // поведение, что зверь вообще что-то умеет; разнобой в именах
            // означает, что для босса придётся помнить исключение.
            BoarBossAnimations.Build();

            int given = 0, missed = 0;

            foreach (var brain in Object.FindObjectsByType<MonsterBrain>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (brain == null) continue;

                var kind = Kinds.FirstOrDefault(k => brain.name.StartsWith(k.Prefix));
                if (string.IsNullOrEmpty(kind.Prefix)) continue;

                var controller =
                    AssetDatabase.LoadAssetAtPath<AnimatorController>(kind.Controller);

                if (controller == null)
                {
                    Debug.LogError("[IsoRPG] Нет контроллера " + kind.Controller);
                    missed++;
                    continue;
                }

                var animator = RealAnimator(brain.gameObject);

                if (animator == null)
                {
                    Debug.LogWarning($"[IsoRPG] У «{brain.name}» не нашёлся аниматор модели.");
                    missed++;
                    continue;
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                EditorUtility.SetDirty(animator);

                var driver = brain.GetComponent<CharacterAnimatorDriver>();

                if (driver != null)
                {
                    driver.SetAttackVariants(kind.Attacks);
                    driver.SetDeathVariants(kind.Deaths);
                    EditorUtility.SetDirty(driver);
                }

                GiveRest(brain.gameObject, kind);

                given++;
            }

            // Праздное поведение — общим правилом: кто понимает `Rest`, тот и
            // получает. Теперь это уже не только босс.
            IdleKit.Apply();

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[IsoRPG] Полные анимации розданы: существ {given}, не вышло {missed}.");
        }

        /// <summary>
        /// Праздное поведение и голос у занятия.
        ///
        /// Список занятий ставим числом, хотя компонент умеет спросить его у
        /// контроллера сам: задание знает точно, из своего же кода, а опрос
        /// оставлен общим правилом на тех, кого мы тут не перечислили.
        ///
        /// Праздное поведение вешает и IdleKit, но он идёт ПОСЛЕ нас — поэтому
        /// компонент создаём сами; он увидит наш и второй раз не поставит.
        /// </summary>
        private static void GiveRest(GameObject beast, Kind kind)
        {
            var idle = beast.GetComponent<IdleBehaviour>();
            if (idle == null) idle = beast.AddComponent<IdleBehaviour>();

            if (kind.Rests != null && kind.Rests.Length > 0) idle.SetKinds(kind.Rests);

            // Длительность ставим числом здесь же: у уже расставленных
            // компонентов в сцене лежат прежние 5–12 с, и правка умолчания в
            // коде их не догонит. Двенадцати не хватало: цепочка «сесть —
            // лечь — спать» и подъём съедают секунд шесть, и зверь буквально
            // ложился на секунду и вставал (Павлон, 02.09.2026).
            idle.SetTiming(new Vector2(10f, 24f), new Vector2(14f, 28f));

            EditorUtility.SetDirty(idle);

            if (kind.HowlKind <= 0) return;

            var voice = beast.GetComponent<IsoRPG.Audio.RestVoice>();
            if (voice == null) voice = beast.AddComponent<IsoRPG.Audio.RestVoice>();

            voice.Setup(kind.HowlKind, IsoRPG.Audio.BeastVoice.WolfHowl);
            EditorUtility.SetDirty(voice);
        }

        /// <summary>
        /// Настоящий аниматор существа, а не пустышка на корне.
        ///
        /// Пустой аниматор попадает на корень сам — от `RequireComponent` у
        /// соседнего компонента, — и перехватывает любой поиск «первого
        /// аниматора в ветке». Признак настоящего: у него есть аватар, то есть
        /// он привязан к скелету модели.
        /// </summary>
        private static Animator RealAnimator(GameObject root)
        {
            var all = root.GetComponentsInChildren<Animator>(true);

            foreach (var animator in all)
                if (animator.avatar != null) return animator;

            // Аватара нет ни у кого — берём самый глубокий: пустышка всегда
            // сидит на корне.
            Animator deepest = null;
            int best = -1;

            foreach (var animator in all)
            {
                int depth = 0;
                for (var t = animator.transform; t != null && t != root.transform; t = t.parent) depth++;

                if (depth > best) { best = depth; deepest = animator; }
            }

            return deepest;
        }
    }
}
