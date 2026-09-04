using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace IsoRPG.Player
{
    /// <summary>
    /// Примерка бега: три варианта по клавише F8.
    ///
    /// Просьба Павла 05.09.2026 после разбора: «повесь мне на свободную F
    /// вариант с бегом ниже скорости и ту анимацию бега, которую предлагаешь».
    ///
    /// Причина, по которой варианта три, а не один: наши 5.5 м/с не совпадают
    /// НИ С ОДНИМ аллюром автора набора. Он рисовал ходьбу под 1.54, бег под
    /// 2.74, спринт под 7.69 — замерено по версиям с корневым движением, и эти
    /// же числа стоят в его собственном контроллере. Наша скорость лежит между
    /// его бегом и спринтом, ближе к спринту. Поэтому любой клип придётся либо
    /// растягивать, либо сжимать, и выбрать это можно только глазами.
    ///
    /// Переключаем ТОЛЬКО скорость и темп ног. Клип не подменяем: подмена
    /// требует своего контроллера и ломает сравнение — менялось бы сразу два
    /// условия, и непонятно, что именно понравилось.
    /// </summary>
    public sealed class RunTryout : MonoBehaviour
    {
        /// <summary>Один вариант: с какой скоростью бежим и как быстро перебираем ногами.</summary>
        private readonly struct Variant
        {
            public readonly string Name;
            public readonly float Speed;
            public readonly float Rate;

            public Variant(string name, float speed, float rate)
            {
                Name = name;
                Speed = speed;
                Rate = rate;
            }
        }

        /// <summary>
        /// Варианты, между которыми выбираем.
        ///
        /// Темп считается от того, что уже собрано в дереве: там клип бега идёт
        /// x2.00, потому что стоит на 5.5 при нарисованных 2.74. Значит
        /// множитель 1.0 — это нынешние x2.00, а 0.5 возвращает клип к его
        /// собственной скорости.
        /// </summary>
        private static readonly Variant[] Variants =
        {
            // Как собрано сейчас: полная скорость, ноги частят вдвое.
            new Variant("быстрый бег, ноги x2.00", 5.5f, 1f),

            // Скорость автора: клип идёт ровно как нарисован, ноги совпадают
            // с землёй идеально. Цена — герой заметно медленнее.
            new Variant("скорость автора 2.74, ноги x1.00", 2.74f, 0.5f),

            // Середина: скорость почти прежняя, ноги частят в полтора раза
            // вместо двух. Тот самый размен, который я советовал.
            new Variant("бег 3.85, ноги x1.40", 3.85f, 0.7f),
        };

        private static readonly int RateHash = Animator.StringToHash("MoveRate");

        private NavMeshAgent agent;
        private Animator animator;
        private int current;

        /// <summary>Сколько ещё показывать подпись варианта, секунды.</summary>
        private float showUntil;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// Поставить первый вариант при запуске.
        ///
        /// Страховка от того, на чём я уже споткнулся: если множитель темпа
        /// окажется нулём (а он им и оказался — значение по умолчанию не
        /// записалось в контроллер), ноги встанут, и герой поедет по земле.
        /// Здесь мы задаём его явно, поэтому одна поломка перестаёт зависеть
        /// от другой.
        /// </summary>
        private void Start() => Apply();

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || !keys.f8Key.wasPressedThisFrame) return;

            current = (current + 1) % Variants.Length;
            Apply();
        }

        private void Apply()
        {
            var variant = Variants[current];

            if (agent != null) agent.speed = variant.Speed;
            if (animator != null) animator.SetFloat(RateHash, variant.Rate);

            showUntil = Time.time + 4f;

            Debug.Log($"[IsoRPG] Примерка бега {current + 1}/{Variants.Length}: {variant.Name}.");
        }

        /// <summary>
        /// Подпись поверх экрана: какой вариант сейчас.
        ///
        /// Без неё сравнение бессмысленно — через три переключения уже не
        /// помнишь, что смотришь. Пишем в углу и гасим через несколько секунд,
        /// чтобы не мешала смотреть на самого героя.
        /// </summary>
        private void OnGUI()
        {
            if (Time.time > showUntil) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
            };

            style.normal.textColor = Color.yellow;

            GUI.Label(new Rect(24f, 60f, 900f, 40f),
                      $"F8 — бег {current + 1}/{Variants.Length}: {Variants[current].Name}", style);
        }
    }
}
