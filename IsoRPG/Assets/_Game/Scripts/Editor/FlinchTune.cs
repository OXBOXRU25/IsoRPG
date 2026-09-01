using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Укорачивает вздрагивание от попаданий у всех, у кого оно есть.
    ///
    /// Замер 01.09.2026 (щуп `boar-probe`): клип реакции длится 1.63 с, выход
    /// стоял на 80% — это 1.42 с вместе со сменой. Кинжал героя бьёт раз в
    /// 1.4 с, то есть зверь находился в реакции ВСЁ время боя, без просвета.
    /// Отсюда обе жалобы Павлона: мелкий кабан «разворачивается боком» (это
    /// поза клипа Malbers, зверь в ней отшатывается) и босс «стоит замерев
    /// между атаками» (его атака в 1.27 с не успевала доиграть).
    ///
    /// Первая попытка лечила ЧАСТОТУ триггера и не помогла: мешала не
    /// частота, а длительность. Правим её.
    ///
    /// Правим по классу, а не у кабанов: состояние зовётся «GetHit» у всех —
    /// у зверей, у героя, у НПС, — и у героя ровно та же беда (его бьют раз
    /// в 2 с при реакции 1.42 с). Чинить одного кабана значило бы вернуться
    /// сюда, как только Павлон заметит то же самое на ком-то ещё.
    /// </summary>
    public static class FlinchTune
    {
        private const string Folder = "Assets/_Game/Art/Animations/Controllers";
        private const string StateName = "GetHit";

        /// <summary>
        /// Сколько длится вздрог. Задаём СЕКУНДАМИ, а не долей клипа.
        ///
        /// Первый заход ставил всем долю 0.25 — и это ровно та ошибка, что
        /// уже описана в своде: правило, выведенное из одного материала,
        /// приложили ко всем. Клипы разной длины: у кабана 1.63 с, у героя
        /// 0.51, у волка 0.62. Доля дала кабану нужные 0.41 с, а герою 0.13
        /// и волку 0.17 — вздрога не видно вовсе. Поймал повторный щуп.
        /// </summary>
        private const float ExitSeconds = 0.40f;

        /// <summary>Смена обратно в ход. Короткая: длинная растягивает ту же позу.</summary>
        private const float BackDuration = 0.10f;

        [MenuItem("Tools/IsoRPG/Бой: укоротить вздрагивание", priority = 39)]
        public static void Apply()
        {
            var text = new StringBuilder("[IsoRPG] Вздрагивание:\n");
            int changed = 0;

            foreach (var path in Directory.GetFiles(Folder, "*.controller", SearchOption.AllDirectories))
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path.Replace('\\', '/'));
                if (controller == null) continue;

                foreach (var layer in controller.layers)
                {
                    var state = layer.stateMachine.states
                                     .FirstOrDefault(s => s.state != null && s.state.name == StateName)
                                     .state;

                    if (state == null) continue;

                    var clip = state.motion as AnimationClip;
                    float length = clip != null ? clip.length : 0f;

                    // Клип короче нужного вздрога — не трогаем: он и так
                    // весь короткий, резать нечего.
                    if (length <= ExitSeconds + 0.05f)
                    {
                        text.Append("  ").Append(controller.name.PadRight(20))
                            .Append("клип ").Append(length.ToString("0.00")).Append(" с — и так короткий, не трогаю\n");
                        continue;
                    }

                    float wanted = Mathf.Clamp(ExitSeconds / length, 0.1f, 0.9f);

                    foreach (var t in state.transitions)
                    {
                        // Трогаем только выход по концу клипа. Переходы по
                        // условию (в смерть, например) не наше дело.
                        if (!t.hasExitTime) continue;

                        text.Append("  ").Append(controller.name.PadRight(20))
                            .Append("клип ").Append(length.ToString("0.00"))
                            .Append(" с, вздрог был ").Append((t.exitTime * length).ToString("0.00"))
                            .Append(" с → стал ").Append((wanted * length).ToString("0.00")).Append(" с\n");

                        t.exitTime = wanted;
                        t.duration = BackDuration;
                        changed++;
                    }

                    EditorUtility.SetDirty(controller);
                }
            }

            AssetDatabase.SaveAssets();

            // Щуп: перечитываем с диска и печатаем, что там теперь на самом
            // деле. Отчёт о правке подтверждает лишь то, что код дошёл до
            // строки.
            var left = Directory.GetFiles(Folder, "*.controller", SearchOption.AllDirectories)
                                .Select(p => AssetDatabase.LoadAssetAtPath<AnimatorController>(p.Replace('\\', '/')))
                                .Where(c => c != null)
                                .SelectMany(c => c.layers.SelectMany(l => l.stateMachine.states))
                                .Where(s => s.state != null && s.state.name == StateName)
                                .SelectMany(s => s.state.transitions.Select(t => new
                                {
                                    t.hasExitTime,
                                    seconds = t.exitTime * ((s.state.motion as AnimationClip)?.length ?? 0f)
                                }))
                                .Count(x => x.hasExitTime && x.seconds > ExitSeconds + 0.1f);

            text.Append("  переходов поправлено ").Append(changed)
                .Append(", осталось длинных ").Append(left);

            Debug.Log(text.ToString());
        }
    }
}
