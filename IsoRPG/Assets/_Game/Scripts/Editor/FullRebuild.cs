using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Один пункт вместо четырёх нажатий.
    ///
    /// Порядок сборки не произвольный, и запомнить его должен код, а не
    /// человек. Банк звуков читают сцены, поэтому он первый; сцены читают
    /// готовые ассеты; игра собирается из сохранённых сцен. Перепутанный
    /// порядок не даёт ошибки — он даёт сборку, в которой не хватает того,
    /// что собрали позже нужного.
    ///
    /// Пункт «Собрать всё» отдельный, а не вместо прежних: когда правка
    /// касается одной сцены, гонять полный круг незачем — он занимает
    /// минуты.
    /// </summary>
    public static class FullRebuild
    {
        [MenuItem("Tools/IsoRPG/СОБРАТЬ ВСЁ И ИГРУ", priority = -10)]
        public static void Everything()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "Сборка сцен требует остановленной игры.", "Понятно");
                return;
            }

            // Спрашиваем один раз: дальше пойдут минуты работы, и прервать
            // её на середине хуже, чем не начинать.
            bool go = EditorUtility.DisplayDialog(
                "Собрать всё?",
                "Будут пересобраны: банк звуков, песочница, главное меню, " +
                "затем игра под Windows." + System.Environment.NewLine +
                System.Environment.NewLine +
                "Несохранённые изменения сцен будут потеряны. Займёт несколько минут.",
                "Поехали", "Отмена");

            if (!go) return;

            // Каждый шаг отдельно и с отчётом: если что-то сломается, из лога
            // должно быть видно, на чём именно, а не «сборка не удалась».
            if (!Step("Банк звуков", SoundBankBuilder.Build)) return;
            if (!Step("Песочница", SandboxSceneBuilder.Build)) return;
            if (!Step("Главное меню", MainMenuBuilder.Build)) return;

            Debug.Log("[IsoRPG] Сцены собраны, начинаю сборку игры.");

            if (!Step("Игра", GameBuilder.BuildGame)) return;

            Debug.Log("[IsoRPG] Готово: собрано всё.");
        }

        /// <summary>
        /// Выполняет шаг и не даёт одному упавшему сборщику утащить остальные.
        ///
        /// Без перехвата исключение на первом шаге останавливает всю цепочку
        /// молча — в консоли будет ошибка про какой-нибудь ассет, и связать
        /// её с тем, что игра не пересобралась, придётся человеку.
        /// </summary>
        private static bool Step(string name, System.Action action)
        {
            try
            {
                Debug.Log("[IsoRPG] Шаг: " + name);
                action();
                return true;
            }
            catch (System.Exception error)
            {
                Debug.LogError("[IsoRPG] Шаг «" + name + "» не прошёл: " + error);

                EditorUtility.DisplayDialog("Сборка остановлена",
                    "Шаг «" + name + "» не прошёл. Подробности в консоли.", "Понятно");

                return false;
            }
        }
    }
}
