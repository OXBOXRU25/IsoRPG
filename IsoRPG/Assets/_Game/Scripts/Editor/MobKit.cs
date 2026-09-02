using UnityEditor;
using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Доносит мобам то, без чего их смерть не обрабатывается вовсе.
    ///
    /// Зачем понадобилось. Волков, кабанов и босса ставят задания `wolves`,
    /// `boars`, `boar-boss` — и они вешают только бой: Health, MeleeCombatant,
    /// MonsterBrain, Targetable. Строитель песочницы вешал монстру сверх того
    /// DeathHandler, Respawner, StunReceiver и полоску над головой, а паки
    /// писались отдельно и этот список не повторили.
    ///
    /// Итог в игре 31.08.2026: «убиваешь моба, а он не умирает — продолжает
    /// бить, и ударить его в ответ уже нельзя». Ровно это и происходит.
    /// Health честно доходит до нуля, но выключать бой, ИИ и навигацию у
    /// трупа некому: DeathHandler — единственное место, где это делается.
    /// Со стороны героя моб уже мёртвый — выбор цели его не берёт (мёртвых
    /// WorldPick не опознаёт), а сам моб об этом не знает и бьёт дальше.
    ///
    /// Добычу здесь НЕ раздаём: таблицы есть только у разбойников
    /// (LT_Bandit и родня), а что падает с волка и кабана — вопрос к Павлу,
    /// а не к коду. Пустая таблица молча оставила бы зверей без дропа, и это
    /// выглядело бы как невезение с шансами, а не как незаданный список.
    /// </summary>
    public static class MobKit
    {
        public static void Apply()
        {
            var all = Object.FindObjectsByType<Targetable>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None);

            int touched = 0;
            int deaths = 0;
            int respawns = 0;
            int stuns = 0;
            int bars = 0;
            int spaces = 0;
            int radii = 0;

            foreach (var target in all)
            {
                // Игрока пропускаем: его смерть и возвращение собирает
                // задание `player-kit`, и там всё иначе — тело не убирается,
                // встаёт он кнопкой.
                if (target.Faction == Faction.Player) continue;

                var go = target.gameObject;

                // Без здоровья умирать нечему: такой Targetable — это
                // сундук или жила, и смерть к ним отношения не имеет.
                if (go.GetComponent<Health>() == null) continue;

                bool changed = false;

                var deathHandler = go.GetComponent<DeathHandler>();

                if (deathHandler == null)
                {
                    deathHandler = go.AddComponent<DeathHandler>();
                    deaths++;
                    changed = true;
                }

                // Тело НЕ топим. Ставим безусловно, а не только при добавлении:
                // компонент мог уже лежать в сцене с прежним значением.
                //
                // По умолчанию труп через 3 секунды уходит под землю со
                // скоростью 0.35 м/с — рассчитано на то, что через шесть
                // секунд его удалят. Но у нас он не удаляется: возрождатель
                // поднимает то же самое тело, и до подъёма проходит полторы
                // минуты. За это время труп опускается на тридцать метров, и
                // воскресает уже под землёй — Павлон 01.09.2026: «кабаны
                // провалились под землю и атакуют меня оттуда». Ровно так это
                // и выглядит: живой моб на глубине, бьющий сквозь грунт.
                var so = new SerializedObject(deathHandler);
                so.FindProperty("sinkBeforeRemoval").boolValue = false;
                so.FindProperty("removeAfter").floatValue = 0f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(deathHandler);

                if (go.GetComponent<StunReceiver>() == null)
                {
                    go.AddComponent<StunReceiver>();
                    stuns++;
                    changed = true;
                }

                // Радиус тела по фактической модели.
                //
                // Дистанция удара считается как «размах + мой радиус + радиус
                // цели», и радиус берётся ОТСЮДА, а не у навигационного
                // агента. У всех зверей он стоял человеческий (0.5), поэтому
                // кабан длиной полтора метра подходил на дистанцию, на
                // которой его туша уже перекрывала героя — со стороны это
                // выглядит как «персонаж залез на кабана».
                var parts = go.GetComponentsInChildren<Renderer>(true);

                if (parts.Length > 0)
                {
                    var box = parts[0].bounds;
                    for (int i = 1; i < parts.Length; i++) box.Encapsulate(parts[i].bounds);

                    // Половина наибольшего горизонтального габарита. Не всю
                    // длину: звери подходят боком не реже, чем мордой.
                    float radius = Mathf.Clamp(Mathf.Max(box.size.x, box.size.z) * 0.5f, 0.4f, 1.6f);

                    var targetSo = new SerializedObject(target);
                    var field = targetSo.FindProperty("bodyRadius");

                    if (field != null && Mathf.Abs(field.floatValue - radius) > 0.05f)
                    {
                        field.floatValue = radius;
                        targetSo.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(target);
                        radii++;
                        changed = true;
                    }
                }

                // Телесное разведение. Было только у героя — а у него вес
                // почти нулевой: по замыслу отходит противник, как в WoW.
                // Отходить оказалось некому, и герой залезал на кабана
                // верхом (Павлон 01.09.2026). Компонент нужен обеим
                // сторонам: он расталкивает пару, а не двигает одного.
                if (go.GetComponent<BodySpace>() == null)
                {
                    go.AddComponent<BodySpace>();
                    spaces++;
                    changed = true;
                }

                // Полоска над головой — только враждебным.
                //
                // Мирным она врёт: над Дозорным у палатки повисла красная
                // полоса, и лагерь стал выглядеть так, будто там стоит враг.
                // Полоска отвечает на вопрос «сколько ему осталось», а он
                // возникает только к тому, кого бьют.
                if (target.Faction == Faction.Hostile)
                {
                    var bar = go.GetComponent<OverheadHealthBar>();

                    if (bar == null)
                    {
                        bar = go.AddComponent<OverheadHealthBar>();
                        bars++;
                        changed = true;
                    }

                    // Размеры проставляем ВСЕГДА, а не только новой полоске:
                    // у расставленных в сцене лежат прежние числа, и правка
                    // умолчания в коде их не догоняет. Полоска вдвое тоньше
                    // прежней плюс ряд комбо-очков под ней (Павлон, 02.09.2026).
                    bar.SetSize(1.1f, 0.065f, 0.032f, 0.012f);
                    EditorUtility.SetDirty(bar);
                    changed = true;
                }
                else
                {
                    // Уже повесили прошлым прогоном — снимаем.
                    var extra = go.GetComponent<OverheadHealthBar>();

                    if (extra != null)
                    {
                        Object.DestroyImmediate(extra);
                        changed = true;
                    }
                }

                if (go.GetComponent<Respawner>() == null)
                {
                    var respawner = go.AddComponent<Respawner>();

                    // Ждать обыска нельзя: добычи у зверей пока нет вовсе,
                    // и возрождение ждало бы обыска, которого не будет
                    // никогда — мир бы просто пустел после первой зачистки.
                    var respawnSo = new SerializedObject(respawner);
                    respawnSo.FindProperty("waitForLooting").boolValue = false;
                    respawnSo.ApplyModifiedPropertiesWithoutUndo();

                    respawns++;
                    changed = true;
                }

                if (changed)
                {
                    touched++;
                    EditorUtility.SetDirty(go);
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Набор мобов: тронуто существ " + touched +
                      " — смерть " + deaths + ", оглушение " + stuns +
                      ", полоска " + bars +
                      ", разведение " + spaces + ", радиус тела " + radii + ", возрождение " + respawns + ".");

            Check();
        }

        /// <summary>
        /// Щуп: спрашиваем сами объекты, а не журнал. Журнал печатает тот же
        /// код, который делал работу, и подтверждает лишь то, что строка
        /// выполнилась.
        /// </summary>
        private static void Check()
        {
            int mobs = 0;
            int withDeath = 0;

            foreach (var target in Object.FindObjectsByType<Targetable>(FindObjectsInactive.Include,
                                                                        FindObjectsSortMode.None))
            {
                if (target.Faction == Faction.Player) continue;
                if (target.GetComponent<Health>() == null) continue;

                mobs++;
                if (target.GetComponent<DeathHandler>() != null) withDeath++;
            }

            if (mobs == withDeath)
                Debug.Log("[IsoRPG] Смерть обрабатывается у всех " + mobs + " существ.");
            else
                Debug.LogError("[IsoRPG] Существ без обработки смерти: " + (mobs - withDeath) +
                               " из " + mobs + " — они останутся бессмертными.");
        }
    }
}
