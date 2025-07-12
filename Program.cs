/*
 * TODO
 * -o arg to order layers descending (lower depth gets drawn on top of higher depth)
 * -O arg to order layers ascending  (higher depth gets drawn on top of lower depth)
 * -f arg to filter out rooms not containing the substring (or regex?)
 */

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
namespace LDTKLevelStitcher
{
    struct Level(int x, int y, int w, int h, int depth) // struct for holding the relevant data from a level when sizing the canvas and placing it on there
    {
        public int x = x, y = y, w = w, h = h, depth = depth;
    };

    internal class Program
    {
        
        static int Main(string[] args)
        {
            int[] version = [0, 1, 1];
            string usageString =
            """
            Usage: LDTKLevelStitcher [-qsoOf] <World file path> <PNG directory path>
            
            Arguments:
                -h, --help: Prints out this help information and exits.
                -v, --version: Prints version information and exits.
                -q, --quiet: Do not output any info text that isn't requested by other switches.
                -s <x>, --scale <x>: Before final output, scale the image down to 1/x its size.
                World file path: The path to the .ldtk file that the PNGs are from.
                PNG directory path: The path to the directory containing the PNG images,
                obtained by telling them to be exported in Project Settings > Extra Files.
            """;
            string helpString =
            """
            This tool takes as input an LDtk world file (.ldtk or .json) and a series of images
            that LDtk can export for each level, and combines them into a single image. The image
            is output as "map.png".

            The tool can optionally scale the resulting image down before exporting, as well as
            ordering the images based on their depth in the editor.
            Warning: This program can probably use multiple gigabytes of RAM on very large maps.

            --------------------


            """ + usageString;
            string worldPath = "";
            string imgPath = "";
            float scaleDenom = 1;
            int ordering = 0; // 0 is no ordering
            bool quiet = false;

            #region arg handling
            int j = 1; // lookahead iterator
            int exit = -1; // -1 means do not exit, anything else means "exit with this code"
            bool noMoreArgs = false;
            if (args.Length == 0)
            {
                Console.WriteLine(usageString);
                return 1;
            }
            else
            {
                foreach (string arg in args)
                {
                    // switches
                    if (arg[0] == '-' && !noMoreArgs)
                    {
                        if (arg.Length == 2 || arg[1] == '-')
                        {
                            switch (arg)
                            {
                                case "-h":
                                case "--help":
                                    Console.WriteLine(helpString);
                                    return 0;
                                case "-v":
                                case "--version":
                                    Console.WriteLine("LDTKLevelStitcher version " + version[0] + "." + version[1] + "." + version[2]);
                                    Console.WriteLine("ImageSharp version 3.1.10");
                                    return 0;
                                case "-q":
                                case "--quiet":
                                    quiet = true;
                                    break;
                                case "-s":
                                case "--scale":
                                    if (!float.TryParse(args[j], out scaleDenom))
                                    {
                                        Console.WriteLine("fatal: Expected number after scale argument, got " + args[j]);
                                        return 1;
                                    }
                                    break;
                                case "--":
                                    noMoreArgs = true;
                                    break;
                                default:
                                    Console.WriteLine("fatal: Unrecognized argument: " + arg);
                                    return 1;
                            }
                        }
                        else foreach (char ltr in arg)
                            {
                                if (ltr == 'h')
                                {
                                    Console.WriteLine(helpString);
                                    return 0;
                                }
                                if (ltr == 'v')
                                {
                                    Console.WriteLine("LDTKLevelStitcher version " + version[0] + "." + version[1] + "." + version[2]);
                                    Console.WriteLine("ImageSharp version 3.1.10");
                                    return 0;
                                }
                                if (ltr == '-') continue;
                                if (ltr == 'q') quiet = true;
                                if (ltr == 's')
                                {
                                    if (!float.TryParse(args[j], out scaleDenom))
                                    {
                                        Console.WriteLine("fatal: Expected number after scale argument, got " + args[j]);
                                        return 1;
                                    }
                                }
                            }
                    }

                    //nonswitches
                    if (File.Exists(arg))
                        worldPath = arg;
                    if (Directory.Exists(arg))
                        imgPath = arg;

                    j++;
                }
            }
            if (worldPath == "" || imgPath == "")
            {
                if (worldPath == "")
                    Console.WriteLine("fatal: World file path does not exist.");
                else
                    Console.WriteLine("fatal: PNG directory does not exist.");
                exit = 1;
            }
            if (exit != -1)
                return exit;
                    #endregion

                    string[] images = Directory.EnumerateFiles(args[1]).ToArray<string>();
                    Dictionary<string, Level> levelInfo = [];

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
                    Utf8JsonReader reader = new(span);

                    bool inLevels = false;
                    string lastIdent = "";
                    Level temp = new(-1, -1, -1, -1, 0);
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
                                    // TODO: add worldDepth here, change count to 5
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
                            // TODO: depth sorting, filtering
                            if (!quiet) Console.WriteLine("Drawing room: " + level.Key);
                            using (Image room = Image.Load(args[1] + level.Key + ".png"))
                                canvas.Mutate(x => x.DrawImage(room, new Point(level.Value.x - borderLeft, level.Value.y - borderTop), 1));
                        }
                        if (scaleDenom != 1)
                            canvas.Mutate(x => x.Resize((int)(canvas.Width / scaleDenom), (int)(canvas.Height / scaleDenom), KnownResamplers.NearestNeighbor));
                        canvas.Save("map.png");
                    }


                    #endregion

                    return 0;
        }
    }
}
