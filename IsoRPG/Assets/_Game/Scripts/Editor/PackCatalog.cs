using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Список наборов, лежащих в проекте.
    ///
    /// Один список на всех, а не по копии в каждом инструменте. Наборы
    /// приезжают и уезжают пачками; пока папки были расписаны отдельно в
    /// починке материалов, отдельно в примерочной и отдельно в голове,
    /// каждый новый набор требовал трёх правок — и третью стабильно
    /// забывали. Теперь добавить набор значит дописать одну строку сюда.
    ///
    /// Все наборы куплены (Павел, 26.08.2026), поэтому вопрос лицензии
    /// закрыт. Табличка на витрине показывает другое, и это важнее для
    /// работы: <see cref="Entry.InGame"/> — набор уже держит игру, его
    /// детали стоят в руинах и на них завязаны сцены; остальные пока
    /// смотрим. Спутать эти две группы дорого: заменить набор, который «уже
    /// в игре», значит переложить всю раскладку.
    /// </summary>
    public static class PackCatalog
    {
        public sealed class Entry
        {
            /// <summary>Папка набора от корня проекта.</summary>
            public string Folder;

            /// <summary>Короткое имя для таблички и меню.</summary>
            public string Title;

            /// <summary>Уже используется в игре, а не просто лежит.</summary>
            public bool InGame;

            /// <summary>Чей набор и что в нём — мелкой строкой на табличке.</summary>
            public string Origin;

            /// <summary>
            /// Ставить ли площадку на витрине.
            ///
            /// Служебные наборы (общая часть Synty, ядро шейдеров) содержат
            /// не декорации, а материалы и заготовки: площадка вышла бы из
            /// кубиков и пустышек. Материалы им чинить надо, показывать —
            /// нечего.
            /// </summary>
            public bool Showcase = true;

            /// <summary>Импортирован ли набор прямо сейчас.</summary>
            public bool Present => AssetDatabase.IsValidFolder(Folder);
        }

        /// <summary>
        /// Порядок — по тому, как их удобно сравнивать: сперва подземелья,
        /// потом жильё и природа. Витрина ставит площадки этим же порядком и
        /// раздаёт клавиши F1…F9 сверху вниз, поэтому первыми стоят те, что
        /// ближе к нашей игре.
        /// </summary>
        public static readonly Entry[] All =
        {
            new Entry
            {
                Folder = "Assets/_Game/Art/KayKit/Dungeon",
                Title  = "KayKit Dungeon",
                InGame = true,
                Origin = "на нём собраны наши руины"
            },
            new Entry
            {
                Folder = "Assets/PolygonDungeon",
                Title  = "POLYGON Dungeons",
                InGame = false,
                Origin = "Synty — подземелья, решено переезжать на него"
            },
            new Entry
            {
                Folder = "Assets/Synty/PolygonDungeonRealms",
                Title  = "POLYGON Dungeon Realms",
                InGame = false,
                Origin = "Synty — подземелья, второе поколение"
            },
            new Entry
            {
                Folder = "Assets/Remesh Games/Stylized Dungeon",
                Title  = "Stylized Dungeon",
                InGame = false,
                Origin = "Remesh Games — рисованные текстуры, другой язык"
            },
            new Entry
            {
                Folder = "Assets/PolygonElvenRealm",
                Title  = "POLYGON Elven Realm",
                InGame = false,
                Origin = "Synty — эльфийские постройки"
            },
            new Entry
            {
                Folder = "Assets/Synty/PolygonFantasyKingdom",
                Title  = "POLYGON Fantasy Kingdom",
                InGame = false,
                Origin = "Synty — город, замок, деревня"
            },
            new Entry
            {
                Folder = "Assets/Synty/PolygonFantasyCharacters",
                Title  = "POLYGON Fantasy Characters",
                InGame = false,
                Origin = "Synty — разбойник, друид, ведьма, колдун, бард, крестьяне"
            },
            new Entry
            {
                Folder = "Assets/PolygonNatureBiomes",
                Title  = "POLYGON Nature Biomes: Meadow Forest",
                InGame = false,
                Origin = "Synty — луг: зелёная трава, кочки, кусты, поля"
            },
            new Entry
            {
                Folder = "Assets/Synty/PolygonParticleFX",
                Title  = "POLYGON Particle FX",
                InGame = false,
                Origin = "Synty — вспышки ударов, магия, огонь, дым"
            },
            new Entry
            {
                Folder = "Assets/Synty/PolygonDungeon",
                Title  = "POLYGON Dungeons 1.10",
                InGame = false,
                Origin = "Synty — подземелья, свежая версия вместо той, что от 2021 года"
            },
            new Entry
            {
                Folder = "Assets/Synty/PolygonHorrorMansion",
                Title  = "POLYGON Horror Mansion",
                InGame = false,
                Origin = "Synty — особняк, мрачная обстановка"
            },
            new Entry
            {
                Folder = "Assets/Synty/PolygonNatureBiomes",
                Title  = "POLYGON Enchanted Forest",
                InGame = false,
                Origin = "Synty — лес, деревья, поляны"
            },
            new Entry
            {
                Folder = "Assets/Ilumisoft/Summer Forest",
                Title  = "Summer Forest",
                InGame = false,
                Origin = "Ilumisoft — летний лес, свой стиль"
            },

            // Служебные и звериные: материалы чиним, площадку не ставим.
            new Entry
            {
                Folder = "Assets/Synty/PolygonGeneric",
                Title  = "POLYGON Generic",
                InGame = false,
                Origin = "общая часть наборов Synty",
                Showcase = false
            },
            new Entry
            {
                Folder = "Assets/Synty/PNB_Core",
                Title  = "PNB Core",
                InGame = false,
                Origin = "ядро шейдеров листвы Synty",
                Showcase = false
            },
            new Entry
            {
                Folder = "Assets/BitGem",
                Title  = "BitGem Ghoul Crew",
                InGame = true,
                Origin = "упыри, уже стоят в склепе",
                Showcase = false
            },
            new Entry
            {
                Folder = "Assets/Polygonal Wolf",
                Title  = "Polygonal Wolf",
                InGame = true,
                Origin = "волки, уже стоят в лесу",
                Showcase = false
            },
        };

        /// <summary>Только те наборы, что сейчас лежат в проекте.</summary>
        public static IEnumerable<Entry> Present => All.Where(e => e.Present);

        /// <summary>Те, под которые витрина ставит площадку.</summary>
        public static IEnumerable<Entry> Shown => Present.Where(e => e.Showcase);

        /// <summary>Папки для починки материалов.</summary>
        public static string[] PresentFolders =>
            Present.Select(e => e.Folder).ToArray();
    }
}
