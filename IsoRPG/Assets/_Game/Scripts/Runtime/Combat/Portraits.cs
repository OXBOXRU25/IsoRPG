using System.Collections.Generic;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Нарисованные портреты вместо снимков с моделей.
    ///
    /// Раньше портрет получался рендером модели: дёшево и всегда совпадает с
    /// тем, кто стоит на карте. Но в круге 60 пикселей модель читается плохо
    /// — это фигурка целиком, а не лицо, и все скелеты в ней одинаковы.
    /// Рисованный портрет решает ровно это: он нарисован под свой размер.
    ///
    /// Сопоставление по имени существа, а не по модели: у нас три вида
    /// скелетов на одной модели и два разбойника с разными лицами. Имя —
    /// то, что видит игрок, и портрет должен совпадать именно с ним.
    /// </summary>
    public static class Portraits
    {
        private const string Folder = "UI/Portraits/";

        /// <summary>
        /// Кто с каким лицом. Ключ — русское имя, оно же ключ перевода:
        /// на английском и украинском существо остаётся тем же самым.
        /// </summary>
        private static readonly Dictionary<string, string> ByName =
            new Dictionary<string, string>
            {
                { "Разбойник", "Rogue" },

                { "Скелет-прислужник", "Skeleton_Warrior" },
                { "Скелет-воин", "Skeleton_Warrior" },
                { "Заблудший скелет", "Skeleton_Warrior" },
                { "Костяной страж", "Skeleton_Warrior" },
                { "Страж склепа", "Skeleton_Warrior" },

                { "Костяной послушник", "Skeleton_Archer" },
                { "Костяной лучник", "Skeleton_Archer" },
                { "Лучник склепа", "Skeleton_Archer" },

                { "Костяной владыка", "Bone_Lord" },

                // Головорез остался без лица намеренно: Павлон 03.09.2026
                // нарисовал новый набор портретов и сказал, что бандит пока
                // не нужен. Старый портрет удалён вместе с остальными
                // заменёнными, новый лежит в исходниках и ждёт своего часа.
                { "Дозорный", "Scout" },
                { "Лазутчик", "Scout" },
                { "Колдун банды", "Warlock" },
                { "Лесной стрелок", "Forest_Archer" },
                { "Атаман Кривой Клык", "Bandit_Chief" },

                { "Кабан", "Boar" },
                { "Вожак кабанов", "Boar_Boss" },
                { "Волк", "Wolf_Grey" },
                { "Гриб-исполин", "Mushroom" },
                { "Талин Кини", "Npc_Guard" },

                // Лошадь портрета до сих пор не имела вовсе — её просто не
                // было в этой таблице, и наведение на неё показывало пустое
                // место. Павлон нарисовал портрет вместе с остальными.
                { "Лошадь", "Horse_Spots" },

                { "Торговец Кувалда", "Merchant" },
                { "Торговец", "Merchant" },
            };

        private static readonly Dictionary<string, Sprite> loaded =
            new Dictionary<string, Sprite>();

        /// <summary>
        /// Портрет по имени существа. Пусто — значит рисованного нет, и
        /// вызывающий волен взять снимок модели.
        /// </summary>
        public static Sprite For(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;

            string file;
            if (!ByName.TryGetValue(displayName, out file)) return null;

            Sprite sprite;
            if (loaded.TryGetValue(file, out sprite)) return sprite;

            sprite = Resources.Load<Sprite>(Folder + file);
            loaded[file] = sprite;

            if (sprite == null)
            {
                Debug.LogWarning("[IsoRPG] Нет портрета " + Folder + file +
                                 " для «" + displayName + "».");
            }

            return sprite;
        }

        /// <summary>
        /// Портрет по имени файла, минуя таблицу имён.
        ///
        /// Для случаев, когда одно имя носят разные с виду существа: масти
        /// волков, окрасы лошадей. Ключ ставится сборщиком на само существо.
        /// </summary>
        public static Sprite ByKey(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;

            if (loaded.TryGetValue(file, out var sprite)) return sprite;

            sprite = Resources.Load<Sprite>(Folder + file);
            loaded[file] = sprite;

            if (sprite == null)
                Debug.LogWarning("[IsoRPG] Нет портрета по ключу " + Folder + file + ".");

            return sprite;
        }

        /// <summary>Портрет собеседницы с квестом — она одна на игру.</summary>
        public static Sprite QuestGiver()
        {
            return Load("Quest_Giver");
        }

        private static Sprite Load(string file)
        {
            Sprite sprite;
            if (loaded.TryGetValue(file, out sprite)) return sprite;

            sprite = Resources.Load<Sprite>(Folder + file);
            loaded[file] = sprite;

            return sprite;
        }
    }
}
