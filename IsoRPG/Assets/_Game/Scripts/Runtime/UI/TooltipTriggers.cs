using UnityEngine;
using UnityEngine.EventSystems;
using IsoRPG.Combat;
using IsoRPG.Items;

namespace IsoRPG.UI
{
    /// <summary>
    /// Общая часть всех подсказок: показать при наведении, спрятать при уходе
    /// и обязательно спрятать, когда элемент выключили.
    ///
    /// Последнее важнее, чем кажется: ячейка сумки исчезает вместе с окном, а
    /// событие ухода курсора при этом не приходит — подсказка о предмете,
    /// который уже не видно, повисает посреди экрана до следующего наведения.
    /// </summary>
    public abstract class TooltipTriggerBase : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        private bool showing;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Tooltip.Instance == null) return;

            showing = Display(Tooltip.Instance, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData) => HideIfMine();

        private void OnDisable() => HideIfMine();

        private void HideIfMine()
        {
            if (!showing) return;

            showing = false;
            if (Tooltip.Instance != null) Tooltip.Instance.Hide();
        }

        /// <summary>Показать содержимое. False — показывать нечего.</summary>
        protected abstract bool Display(Tooltip tooltip, Vector2 at);
    }

    /// <summary>Подсказка о предмете: сумка, слоты экипировки, окно добычи.</summary>
    public sealed class ItemTooltipTrigger : TooltipTriggerBase
    {
        private ItemDefinition item;
        private Experience experience;

        public void Setup(ItemDefinition definition, Experience level)
        {
            item = definition;
            experience = level;
        }

        protected override bool Display(Tooltip tooltip, Vector2 at)
        {
            if (item == null) return false;

            tooltip.ShowItem(item, experience != null ? experience.Level : 1, at);
            return true;
        }
    }

    /// <summary>Подсказка о приёме: панель способностей.</summary>
    public sealed class AbilityHoverTrigger : TooltipTriggerBase
    {
        private AbilityDefinition ability;
        private WeaponStats weapon;

        public void Setup(AbilityDefinition definition, WeaponStats stats)
        {
            ability = definition;
            weapon = stats;
        }

        protected override bool Display(Tooltip tooltip, Vector2 at)
        {
            if (ability == null) return false;

            tooltip.ShowAbility(ability, weapon != null ? weapon.WeaponDamage : 0, at);
            return true;
        }
    }

    /// <summary>
    /// Простая подсказка из двух строк: кнопки панели, пустые слоты.
    /// </summary>
    public sealed class TextTooltipTrigger : TooltipTriggerBase
    {
        private string caption;
        private string hint;

        public void Setup(string title, string text)
        {
            caption = title;
            hint = text;
        }

        protected override bool Display(Tooltip tooltip, Vector2 at)
        {
            if (string.IsNullOrEmpty(caption)) return false;

            tooltip.ShowText(caption, hint, at);
            return true;
        }
    }
}
