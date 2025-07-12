using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;
using System.Text.Json;
namespace LDTKLevelStitcher
{
    struct Level // struct for holding the relevant data from a level when sizing the canvas and placing it on there
    {
        public Level(int xx, int yy, int ww, int hh)
        {
            x = xx;
            y = yy;
            w = ww;
            h = hh;
        }
        public int x, y, w, h;
    };

    internal class Program
    {
        static int[] version = [0, 1, 0];
        static string helpString = 
        """
            Usage: LDTKLevelStitcher <World file path> <PNG directory path>
            
            Arguments:
              -h, --help: Prints out this help information and exits.
              -v, --version: Prints version information and exits.
              World file path: The path to the .ldtk file that the PNGs are from.
              PNG directory path: The path to the directory containing the PNG images,
              obtained by telling them to be exported in Project Settings > Extra Files.
        """;
        static int Main(string[] args)
        {
            #region arg handling
            switch (args.Length)
            {
                case 0:
                    Console.WriteLine(helpString);
                    return 1;
                case 1:
                    if (args[0] == "-h" || args[0] == "--help")
                        Console.WriteLine(helpString);
                    else if (args[0] == "-v" || args[0] == "--version")
                    {
                        Console.WriteLine("LDTKLevelStitcher version " + version[0] + "." + version[1] + "." + version[2]);
                        Console.WriteLine("ImageSharp version 3.1.10");
                    }
                    else
                    {
                        if (File.Exists(args[0]))
                            Console.WriteLine("fatal: PNG directory not provided");
                        else if (args[0][0] == '-')
                            Console.WriteLine("fatal: Unrecognized argument: " + args[0]);
                        else
                            Console.WriteLine("fatal: File does not exist: " + args[0]);
                        return 1;
                    }
                    return 0;
                case 2:
                    if (!File.Exists(args[0]))
                    {
                        Console.WriteLine("fatal: World file does not exist: " + args[0]);
                        return 1;
                    }
                    if (!Directory.Exists(args[1]))
                    {
                        Console.WriteLine("fatal: PNG directory does not exist: " + args[1]);
                        return 1;
                    }
                    break;
                default:
                    Console.WriteLine("fatal: Too many arguments provided. Expected 0-2, got " + args.Length);
                    Console.WriteLine();
                    Console.WriteLine(helpString);
                    return 1;
            }
            #endregion

            string[] images = Directory.EnumerateFiles(args[1]).ToArray<string>();
            Dictionary<string, Level> levelInfo = new Dictionary<string, Level>();

            #region json deserializing

            /* why did i think this was necessary lmao?
             * 
             * 
             * the normal json reader is made for deserializing into classes, but we want to work with raw data.
             * for this we're supposed to use Utf8JsonReader, which means we need to convert the array of directories
             * into UTF8.
             * /
            static byte[][] images;
            images = new byte[images_u16.Length][];
            int i = 0;
            foreach (string ln in images_u16)
            {
                images[i] = Encoding.UTF8.GetBytes(images_u16[i]);
                i++;
            }
            */

            ReadOnlySpan<byte> span = File.ReadAllBytes(args[0]);
            Utf8JsonReader reader = new Utf8JsonReader(span);

            bool inLevels = false;
            string lastIdent = "";
            Level temp = new(-1, -1, -1, -1);
            int count = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (!inLevels && reader.GetString() == "levels")
                    {
                        inLevels = true;
                        continue;
                    }
                    if (inLevels && reader.GetString() == "identifier")
                    {
                        reader.Read();
                        lastIdent = reader.GetString();
                        count = 0;
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.PropertyName && reader.GetString() == "worldX")
                            {
                                reader.Read();
                                temp.x = reader.GetInt32();
                                count++;
                            }
                            if (reader.TokenType == JsonTokenType.PropertyName && reader.GetString() == "worldY")
                            {
                                reader.Read();
                                temp.y = reader.GetInt32();
                                count++;
                            }
                            if (reader.TokenType == JsonTokenType.PropertyName && reader.GetString() == "pxWid")
                            {
                                reader.Read();
                                temp.w = reader.GetInt32();
                                count++;
                            }
                            if (reader.TokenType == JsonTokenType.PropertyName && reader.GetString() == "pxHei")
                            {
                                reader.Read();
                                temp.h = reader.GetInt32();
                                count++;
                            }
                            if (count == 4)
                            {
                                levelInfo[lastIdent] = temp;
                                break;
                            }
                        }
                    }
                }
            }
            #endregion

            #region image generation
            int borderTop = int.MaxValue;
            int borderBottom = 0;
            int borderLeft = int.MaxValue;
            int borderRight = 0;

            foreach (KeyValuePair<string, Level> level in levelInfo)
            {
                //    Console.WriteLine(level.Key + ": {" + level.Value.x + ", " + level.Value.y + ", " + level.Value.w + ", " + level.Value.h + "}");
                if (level.Value.x < borderLeft)
                    borderLeft = level.Value.x;
                if (level.Value.x + level.Value.w > borderRight)
                    borderRight = level.Value.x + level.Value.w;
                if (level.Value.y < borderTop)
                    borderTop = level.Value.y;
                if (level.Value.y + level.Value.h > borderBottom)
                    borderBottom = level.Value.y + level.Value.h;
            }

            using (Image<Rgba32> canvas = new(borderRight - borderLeft, borderBottom - borderTop))
            {
                foreach (KeyValuePair<string, Level> level in levelInfo)
                {
                    Console.WriteLine("Drawing room: " + level.Key);
                    using (Image room = Image.Load(args[1] + level.Key + ".png"))
                        canvas.Mutate(x => x.DrawImage(room, new Point(level.Value.x - borderLeft, level.Value.y - borderTop), 1));
                }
                    canvas.Save("map.png");
            }


                #endregion

                return 0;
        }
    }
}

/*
- handle args
- iterate all levels and store:
  - a mapping of name: x, y, w, h
  - minimum x (left boundary)
  - minimum y (top boundary)
  - maximum x + w (right boundary)
  - maximum y + h (bottom boundary)
*/