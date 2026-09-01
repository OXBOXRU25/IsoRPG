using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using IsoRPG.Player;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Переводит героя с навигационной сетки на физическую капсулу — и обратно.
    ///
    /// Ставится и снимается одним заданием намеренно: схема движения меняет
    /// ощущение игры целиком, и решать это Павлону, глядя на обе. Пока обе
    /// живут рядом, откат стоит одной строки в очереди, а не дня работы.
    ///
    /// Что происходит: на героя добавляется <c>CharacterController</c> и
    /// <c>PlayerMotor</c>. Агент остаётся — он считает путь, — но позицию
    /// больше не трогает. <c>GroundHug</c> выключается: он держал героя на
    /// земле, подпирая агента, а капсула стоит на земле сама.
    /// </summary>
    public static class HeroPhysics
    {
        /// <summary>Рост героя. Тот же, что у капсулы-заглушки, чтобы камера и прицел не поехали.</summary>
        private const float Height = 1.9f;

        private const float Radius = 0.35f;

        /// <summary>Высота перешагивания. Ниже — герой спотыкается о бордюр, выше — залезает на заборы.</summary>
        private const float Step = 0.4f;

        /// <summary>Предел подъёма. Крутизна навигации поднята до 55°, капсуле оставляем чуть меньше.</summary>
        private const float Slope = 50f;

        [MenuItem("Tools/IsoRPG/Герой: физическая капсула", priority = 34)]
        public static void On() => Apply(true);

        [MenuItem("Tools/IsoRPG/Герой: вернуть навигацию", priority = 35)]
        public static void Off() => Apply(false);

        private static void Apply(bool physics)
        {
            var player = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                               .FirstOrDefault(g => g.name == "Player");

            if (player == null)
            {
                Debug.LogWarning("[IsoRPG] Игрока в сцене нет — физику ставить некому.");
                return;
            }

            var agent = player.GetComponent<NavMeshAgent>();
            var hug = player.GetComponents<MonoBehaviour>().FirstOrDefault(m => m != null && m.GetType().Name == "GroundHug");

            if (physics)
            {
                var body = player.GetComponent<CharacterController>();
                if (body == null) body = player.AddComponent<CharacterController>();

                body.height = Height;
                body.radius = Radius;
                body.center = new Vector3(0f, Height * 0.5f, 0f);
                body.stepOffset = Step;
                body.slopeLimit = Slope;
                // Зазор, на который капсуле разрешено вдавливаться в стену.
                // Ноль даёт дрожание на касании, слишком много — видимый отступ.
                body.skinWidth = 0.03f;

                if (player.GetComponent<PlayerMotor>() == null) player.AddComponent<PlayerMotor>();

                if (agent != null)
                {
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                }

                // Прижим к грунту подпирал агента снизу. С капсулой он спорит
                // с физикой: та ставит героя на коллайдер, этот тянет к сетке.
                if (hug != null) hug.enabled = false;
            }
            else
            {
                var motor = player.GetComponent<PlayerMotor>();
                if (motor != null) Object.DestroyImmediate(motor);

                var body = player.GetComponent<CharacterController>();
                if (body != null) Object.DestroyImmediate(body);

                if (agent != null)
                {
                    agent.updatePosition = true;
                    agent.updateRotation = true;
                }

                if (hug != null) hug.enabled = true;
            }

            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveOpenScenes();

            // Щуп: читаем, что получилось на самом деле, а не что задумывали.
            var check = player.GetComponent<CharacterController>();
            Debug.Log(
                $"[IsoRPG] Герой: {(physics ? "физическая капсула" : "навигация")}. Сцена {player.scene.name}\n" +
                $"  CharacterController: {(check != null ? $"есть, рост {check.height:F2} м, радиус {check.radius:F2}, шаг {check.stepOffset:F2}, склон {check.slopeLimit:F0}°" : "нет")}\n" +
                $"  PlayerMotor: {(player.GetComponent<PlayerMotor>() != null ? "есть" : "нет")}\n" +
                $"  агент двигает позицию: {(agent != null ? agent.updatePosition.ToString() : "агента нет")}\n" +
                $"  GroundHug: {(hug != null ? (hug.enabled ? "включён" : "выключен") : "нет")}");
        }
    }
}
