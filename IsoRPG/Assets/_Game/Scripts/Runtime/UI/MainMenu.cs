using UnityEngine;
using UnityEngine.SceneManagement;

namespace IsoRPG.UI
{
    /// <summary>
    /// Главное меню.
    ///
    /// Отдельная сцена, а не панель поверх игры. Так игра начинается с
    /// экрана, а не с уже идущего боя: пока меню — часть игровой сцены,
    /// мир за ним живёт, монстры ходят, музыка боя играет, и «Начать игру»
    /// означает всего лишь «убрать картинку».
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [Tooltip("Какую сцену запускать по кнопке.")]
        /// <summary>
        /// Какую сцену грузит «Начать игру».
        ///
        /// Переехало с «Sandbox» на «Arena»: старая песочница набрала слоями
        /// годовой мусор и чинить внутри неё нельзя — каждая проверка идёт в
        /// окружении, где сломано ещё пять вещей. Она остаётся на диске как
        /// откат и склад, но игра начинается с новой.
        /// </summary>
        [SerializeField] private string gameScene = "Arena";

        public void SetGameScene(string scene) => gameScene = scene;

        public void StartGame()
        {
            IsoRPG.Audio.Sfx.OpenWindow();
            SceneManager.LoadScene(gameScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            // В редакторе выхода нет: просто останавливаем игру, иначе
            // кнопка выглядит сломанной при проверке.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
