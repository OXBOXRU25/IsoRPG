using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using IsoRPG.Combat;
using IsoRPG.Player;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп: что за анимации у существ на сцене и всё ли на месте.
    ///
    /// Заведён потому, что журнал сборки печатает тот же код, который делал
    /// работу, и подтверждает лишь, что код дошёл до строки. Здесь наоборот:
    /// читаем РЕЗУЛЬТАТ — контроллер, который лежит на звере, — и ругаемся,
    /// если чего-то не хватает.
    ///
    /// Проверяем ровно то, на чём уже обжигались:
    ///   - серия ударов у зверя и число, которое ему проставили в бою: они
    ///     обязаны совпасть, иначе часть ударов уходит в пустоту;
    ///   - есть ли состояния `Rest_N` под каждое занятие, которое ему назвали;
    ///   - висит ли голос на уроне и смерти;
    ///   - не остался ли на корне пустой аниматор, перехватывающий поиск.
    /// </summary>
    public static class BeastProbe
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        [MenuItem("Tools/IsoRPG/Щуп: анимации существ", priority = 48)]
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            var report = new StringBuilder();
            int trouble = 0;

            // Считаем по видам, а не по каждому зверю: десять кабанов дают
            // десять одинаковых строк и прячут единственную важную.
            var seen = new System.Collections.Generic.HashSet<string>();

            foreach (var brain in Object.FindObjectsByType<MonsterBrain>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .OrderBy(b => b.name))
            {
                if (brain == null) continue;

                var animator = brain.GetComponentsInChildren<Animator>(true)
                                    .FirstOrDefault(a => a.runtimeAnimatorController != null);

                string controllerName = animator?.runtimeAnimatorController?.name ?? "НЕТ";

                // Вид: имя без номера. «Кабан 3» и «Кабан 7» — один вид.
                string kind = controllerName;
                if (!seen.Add(kind)) continue;

                report.Append("\n  ").Append(brain.name).Append(" — контроллер ").Append(controllerName);

                if (animator == null || animator.runtimeAnimatorController == null)
                {
                    report.Append("  ← КОНТРОЛЛЕРА НЕТ");
                    trouble++;
                    continue;
                }

                // Пустой аниматор на корне перехватывает поиск «первого в
                // ветке» — на этом мы уже стояли 02.09.2026.
                int empties = brain.GetComponentsInChildren<Animator>(true)
                                   .Count(a => a.runtimeAnimatorController == null);

                if (empties > 0)
                {
                    report.Append("  ← ЛИШНИХ ПУСТЫХ АНИМАТОРОВ: ").Append(empties);
                    trouble++;
                }

                var asset = animator.runtimeAnimatorController as AnimatorController;

                if (asset == null)
                {
                    report.Append("  (не читается как ассет)");
                    continue;
                }

                var states = asset.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();

                int attacks = 0;
                while (states.Contains("Attack_" + (attacks + 1))) attacks++;

                var rests = Enumerable.Range(1, 6).Where(i => states.Contains("Rest_" + i)).ToArray();

                report.Append("\n      состояний ").Append(states.Length)
                      .Append(", ударов в серии ").Append(attacks)
                      .Append(", занятий ").Append(rests.Length == 0 ? "нет" : string.Join("/", rests))
                      .Append(", клипов в контроллере ").Append(asset.animationClips.Length);

                // Число, которое видит бой. Оно и решает, какой номер придёт
                // в аниматор, — а не то, сколько состояний собрано.
                var driver = brain.GetComponent<CharacterAnimatorDriver>();
                int told = driver != null ? driver.AttackVariants : -1;

                if (driver == null)
                {
                    report.Append("\n      ← НЕТ ВОДИТЕЛЯ АНИМАЦИЙ");
                    trouble++;
                }
                else if (attacks > 0 && told != attacks)
                {
                    report.Append("\n      ← БОЮ СКАЗАНО ").Append(told)
                          .Append(" УДАРОВ, А СОБРАНО ").Append(attacks)
                          .Append(" — часть ударов уйдёт в пустоту");
                    trouble++;
                }

                var idle = brain.GetComponent<IdleBehaviour>();

                if (rests.Length > 0 && idle == null)
                {
                    report.Append("\n      ← ЗАНЯТИЯ СОБРАНЫ, А ПРАЗДНОГО ПОВЕДЕНИЯ НЕТ");
                    trouble++;
                }

                var hurt = brain.GetComponent<IsoRPG.Audio.CreatureVoice>();
                var howl = brain.GetComponent<IsoRPG.Audio.RestVoice>();

                report.Append("\n      голос: урон/смерть ").Append(hurt != null ? "есть" : "НЕТ")
                      .Append(", голос занятия ").Append(howl != null ? "есть" : "нет");

                if (hurt == null) trouble++;
            }

            // Банк звуков: какие наборы пустые. Пустой набор — это не ошибка,
            // а честный список того, что ещё не сгенерировано.
            var bank = SoundBankBuilder.Load();

            if (bank != null)
            {
                var empty = typeof(IsoRPG.Audio.SoundBank).GetFields()
                    .Where(f => f.FieldType == typeof(AudioClip[]))
                    .Where(f => (f.GetValue(bank) as AudioClip[])?.Length is null or 0)
                    .Select(f => f.Name)
                    .ToArray();

                report.Append("\n\n  Пустых наборов в банке звуков: ").Append(empty.Length);
                if (empty.Length > 0) report.Append(" — ").Append(string.Join(", ", empty));
            }

            string verdict = trouble == 0 ? "всё на месте" : "НЕПОРЯДКА: " + trouble;

            Debug.Log("[IsoRPG] Щуп анимаций существ — " + verdict + ":" + report);
        }
    }
}
