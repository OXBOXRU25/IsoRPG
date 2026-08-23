using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Планировка руин, записанная картой.
    ///
    /// Карта нарисована текстом, потому что её надо ЧИТАТЬ и править глазом.
    /// Список координат не рассказывает, где зал, а где двор; карта
    /// рассказывает с первого взгляда, и меняется без единой строчки кода.
    ///
    /// СЛОВАРЬ СИМВОЛОВ
    ///
    ///   пробел  снаружи, ничего
    ///   #       стена (углы и трещины подставляются сами)
    ///   W       стена с открытым окном — сквозь неё видно
    ///   T       стена с факелом
    ///   D       проём, через него ходят
    ///   .       каменный пол
    ///   ,       земляной пол (двор, дорожки)
    ///   o       колонна        x  обломки
    ///   b       бочка          k  ящики
    ///   c       сундук         l  стол
    ///   s       полка          h  табурет
    ///
    /// Правила, которые стоит держать в голове, рисуя:
    ///   • у каждой комнаты должен быть хотя бы один проём, иначе туда не
    ///     войти — навигация просто не найдёт пути;
    ///   • разрывы в стенах важнее самих стен: они дают укрытия от стрел и
    ///     пути обхода, то есть работают на бой, а не только на вид;
    ///   • земляной пол по кромке смягчает переход от камня к траве, без
    ///     него постройка обрывается ножом.
    /// </summary>
    public static class RuinsLayout
    {
        /// <summary>
        /// План: главный зал с колоннами, малая комната-хранилище справа,
        /// разрушенная пристройка сверху, дорожки во двор.
        /// </summary>
        public static readonly string[] Map =
        {
            "     ,,,,,,,,,,      ",
            "    ,,#WTDDTW#,,     ",
            "   ,,#l.......s#,,   ",
            "   ,#T..o...o..T#,   ",
            "   ,W...........W,   ",
            "  ,,D.....l.....D,,  ",
            "   ,W...........W,   ",
            "   ,#T..o...o..T#,   ",
            "   ,,#s.......k#,,   ",
            "    ,,#W#DD#W#,,,,,  ",
            "     ,,,#..#,,,,,,,  ",
            "      ,,#..#####,,,  ",
            "      ,,#......W#,,  ",
            "     ,,,D..c...b#,,  ",
            "      ,,#h.l..k.#,   ",
            "      ,,#WW#DD#W#,   ",
            "       ,,,,,,,,,,,   ",
        };

        private const string WallModel = "wall";
        private const string WallCracked = "wall_cracked";
        private const string WallBroken = "wall_broken";
        private const string WallCorner = "wall_corner";
        private const string WallWindow = "wall_archedwindow_open";
        private const string DoorwayModel = "wall_doorway";
        private const string TorchWall = "torch_mounted";
        private const string PillarModel = "pillar";
        private const string FloorStone = "floor_tile_large";
        private const string FloorDirt = "floor_dirt_large";

        /// <summary>Есть ли на клетке пол.</summary>
        public static bool HasFloor(char c) => c != ' ';

        public static string FloorFor(char c) =>
            c == ',' || c == 'x' ? FloorDirt : FloorStone;

        /// <summary>Стена, если клетка — стена. Иначе пусто.</summary>
        public static string WallFor(char c)
        {
            switch (c)
            {
                case '#': return WallModel;
                case 'W': return WallWindow;
                case 'T': return WallModel;      // факел ставится сверху
                case 'D': return DoorwayModel;
                default:  return null;
            }
        }

        public static bool IsWallChar(char c) =>
            c == '#' || c == 'W' || c == 'T' || c == 'D';

        /// <summary>Проходимая клетка — по ней ходят и на ней стоит мебель.</summary>
        public static bool IsOpen(char c) =>
            c == '.' || c == ',' || c == 'o' || c == 'x' ||
            c == 'b' || c == 'k' || c == 'c' || c == 'l' ||
            c == 's' || c == 'h';

        public static string PropFor(char c)
        {
            switch (c)
            {
                case 'o': return PillarModel;
                case 'x': return "rubble_half";
                case 'b': return "barrel_large";
                case 'k': return "crates_stacked";
                case 'c': return "chest";
                case 'l': return "table_long_broken";
                case 's': return "shelf_small_candles";
                case 'h': return "stool";
                default:  return null;
            }
        }

        /// <summary>Крупный ли предмет — такой перекрывает выстрел.</summary>
        public static bool IsSolidProp(char c) => c == 'o' || c == 'k';

        public static bool HasTorch(char c) => c == 'T';
        public static string TorchModel => TorchWall;
        public static string CornerModel => WallCorner;
        public static string CrackedModel => WallCracked;
        public static string BrokenModel => WallBroken;

        /// <summary>
        /// Каждая пятая стена потрескалась. Не случайность ради случайности:
        /// одинаковые секции по всему периметру читаются как обои, а нам
        /// нужны руины, простоявшие столетие.
        /// </summary>
        public static bool ShouldCrack(int x, int z) => ((x * 5 + z * 3) % 5) == 0;

        /// <summary>Каждая девятая — обрушена совсем.</summary>
        public static bool ShouldBreak(int x, int z) => ((x * 7 + z * 11) % 9) == 0;
    }
}
