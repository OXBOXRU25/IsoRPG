using System.Text;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Пишет в журнал игры, в каком состоянии находится аниматор существа.
    ///
    /// Заведён 02.09.2026: Павлон дважды сказал «у босса не вижу новых
    /// анимаций», а все замеры в редакторе показывали, что контроллер на
    /// месте и полон — 22 состояния, 15 параметров. Спорить с заказчиком
    /// бессмысленно: он смотрит на игру, а я на редактор. Пусть игра сама
    /// скажет, что играет.
    ///
    /// Печатает только при СМЕНЕ состояния, а не каждый кадр: журнал должен
    /// читаться глазами, а не пролистываться.
    ///
    /// Щуп временный. Снимается заданием `anim-log-off`, когда разберёмся.
    /// </summary>
    public sealed class AnimatorProbe : MonoBehaviour
    {
        [Tooltip("Как часто заглядывать. Реже — дешевле.")]
        [SerializeField] private float every = 0.25f;

        private Animator animator;
        private float next;
        private int lastState;
        private string who;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>(true);

            var target = GetComponent<Targetable>();
            who = target != null ? target.DisplayName : name;
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (Time.time < next) return;

            next = Time.time + every;

            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == lastState) return;

            lastState = info.shortNameHash;

            var line = new StringBuilder("[Щуп] ").Append(who).Append(": состояние ");

            // Имя состояния в сборке недоступно — только хеш. Сверяем с теми,
            // что нас интересуют, и печатаем по-человечески.
            line.Append(Name(info.shortNameHash));

            foreach (var p in animator.parameters)
            {
                switch (p.type)
                {
                    case AnimatorControllerParameterType.Float:
                        float f = animator.GetFloat(p.nameHash);
                        if (Mathf.Abs(f) > 0.01f) line.Append(", ").Append(p.name).Append('=').Append(f.ToString("0.00"));
                        break;

                    case AnimatorControllerParameterType.Int:
                        int i = animator.GetInteger(p.nameHash);
                        if (i != 0) line.Append(", ").Append(p.name).Append('=').Append(i);
                        break;

                    case AnimatorControllerParameterType.Bool:
                        if (animator.GetBool(p.nameHash)) line.Append(", ").Append(p.name);
                        break;
                }
            }

            Debug.Log(line.ToString());
        }

        private static string Name(int hash)
        {
            foreach (var known in Known)
                if (Animator.StringToHash(known) == hash) return known;

            return "неизвестное (" + hash + ")";
        }

        private static readonly string[] Known =
        {
            "Locomotion", "LocomotionCombat", "Statue_1", "Statue_2", "Statue_3",
            "IdleBreak", "Stunned", "Buff", "Jump", "Death", "Death_Squash",
            "Attack_1", "Attack_2", "Attack_3", "Attack_4", "Attack_5", "Attack_6", "Attack_7",
            "GetHit", "GetHit_Front", "GetHit_Back", "GetHit_Left", "GetHit_Right",
            "Block_1", "Block_2", "Block_3", "Charge_Start", "Charge_Hold", "Charge_End",
            "Circle_Left", "Circle_Right", "Eat", "Sit", "Sleep",
        };
    }
}
