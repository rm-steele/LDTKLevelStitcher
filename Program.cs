using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            int[] version = [1, 1, 0];
            string usageString =
            """
            Usage: LDTKLevelStitcher [-qsfoO] <World file path> <PNG directory path>
            
            Arguments:
                -h, --help: Prints out this help information and exits.
                -v, --version: Prints version information and exits.
                -q, --quiet: Do not output any info text that isn't requested by other switches.
                --verbose: Print extra information used for debugging.
                -s <x>, --scale <x>: Before final output, scale the image down to 1/x its size.
                -f <regex>, --filter <regex>: Only include levels matching the specified regex 
                  string. regex101.com is good at helping build these if you aren't familiar.
                -o: Order rooms by depth, with rooms of a smaller depth value drawing over larger values.
                -O: Order rooms by depth, with rooms of a larger depth value drawing over smaller values.
                -i ####x####: Force the output image to be a certain size, with trailing space being added to
                  or removed from the bottom and right sides. Formatted as <NUMBER1>x<NUMBER2> with no spaces.
                  Use 0 to leave the size unchanged in that axis.
                -- : Stop porcessing arguments. For if your file path begins with - for some reason.
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
            int ordering = 0; // 0 is no ordering, 1 is ascending, -1 is descending
            int verbosity = 1; // 0 = quiet, 2 = loud
            string filter = "";
            int[] imgSize = [0, 0];

            #region arg handling
            int j = 1; // lookahead iterator
            bool noMoreArgs = false;
            bool parameter = false; // next argument is a paramter, don't try to process it as a switch
            // Console.WriteLine("Starting arg handling");
            if (args.Length == 0)
            {
                Console.WriteLine(usageString);
                return 1;
            }
            else
            {
                // Console.WriteLine("Starting arg iteration");
                foreach (string arg in args)
                {
                    if (verbosity > 1) Console.WriteLine("Parsing arg " + arg + " which is arg number " + j + " of " + args.Length); // yes, this won't show for all args because it needs to parse --verbose first
                    // switches
                    if (arg[0] == '-' && !noMoreArgs && !parameter)
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
                                    verbosity--;
                                    break;
                                case "-s":
                                case "--scale":
                                    if (j == args.Length)
                                    {
                                        Console.WriteLine("fatal: No parameter follows argument " + arg);
                                        return 1;
                                    }
                                    if (!float.TryParse(args[j], out scaleDenom))
                                    {
                                        Console.WriteLine("fatal: Expected number after scale argument, got " + args[j]);
                                        return 1;
                                    }
                                    parameter = true;
                                    break;
                                case "-f":
                                case "--filter":
                                    if (j == args.Length)
                                    {
                                        Console.WriteLine("fatal: No parameter follows argument " + arg);
                                        return 1;
                                    }
                                    filter = args[j];
                                    parameter = true;
                                    break;
                                case "-o":
                                    ordering--;
                                    break;
                                case "-O":
                                    ordering++;
                                    break;
                                case "--verbose":
                                    verbosity++;
                                    break;
                                case "-i":
                                    if (j == args.Length)
                                    {
                                        Console.WriteLine("fatal: No parameter follows argument " + arg);
                                        return 1;
                                    }
                                    var success = true;
                                    success &= int.TryParse(args[j].Split('x')[0], out imgSize[0]);
                                    success &= int.TryParse(args[j].Split('x')[1], out imgSize[1]);
                                    if (!success)
                                    {
                                        Console.WriteLine("fatal: Failed to parse parameter as size string: " + args[j]);
                                        return 1;
                                    }
                                    parameter = true;
                                    break;
                                case "--":
                                    noMoreArgs = true;
                                    break;
                                default:
                                    Console.WriteLine("fatal: Unrecognized argument: " + arg);
                                    return 1;
                            }
                        }
                        else
                        {
                            if (verbosity > 1) Console.WriteLine("Starting aggreagted arg handling");
                            foreach (char ltr in arg)
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
                                if (ltr == 'q') verbosity--;
                                if (ltr == 'o') ordering--;
                                if (ltr == 'O') ordering++;
                                if (ltr == 's')
                                {
                                    if (j == args.Length)
                                    {
                                        Console.WriteLine("fatal: No parameter follows argument " + arg);
                                        return 1;
                                    }
                                    if (!float.TryParse(args[j], out scaleDenom))
                                    {
                                        Console.WriteLine("fatal: Expected number after scale argument, got " + args[j]);
                                        return 1;
                                    }
                                    parameter = true;
                                }
                                if (ltr == 'f')
                                {
                                    if (j == args.Length)
                                    {
                                        Console.WriteLine("fatal: No parameter follows argument " + arg);
                                        return 1;
                                    }
                                    filter = args[j];
                                    parameter = true;
                                }
                                if (ltr == 'i')
                                {
                                    if (j == args.Length)
                                    {
                                        Console.WriteLine("fatal: No parameter follows argument " + arg);
                                        return 1;
                                    }
                                    var success = true;
                                    success &= int.TryParse(args[j].Split('x')[0], out imgSize[0]);
                                    success &= int.TryParse(args[j].Split('x')[1], out imgSize[1]);
                                    if (!success)
                                    {
                                        Console.WriteLine("fatal: Failed to parse parameter as size string: " + args[j]);
                                        return 1;
                                    }
                                    parameter = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        parameter = false;
                    }
                    if (verbosity > 1) Console.WriteLine("Leaving switch handler");

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
                return 1;
            }
            if (imgPath[^1] != '/' && imgPath[^1] != '\\')
            {
                imgPath += '/';
            }
            #endregion

            if (verbosity > 1) Console.WriteLine("Enumerating PNGs");
            string[] images = Directory.EnumerateFiles(imgPath).ToArray<string>();
            Dictionary<string, Level> levelInfo = [];


            #region json deserializing
            ReadOnlySpan<byte> span = File.ReadAllBytes(worldPath);
            Utf8JsonReader reader = new(span);

            bool inLevels = false;
            string lastIdent = "";
            Level temp = new(-1, -1, -1, -1, 0);
            int count = 0;
            if (verbosity > 1) Console.WriteLine("Starting JSON parse");
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
                        if (filter != "")
                        {
                            if (Regex.Match(lastIdent, filter) == Match.Empty)
                                continue;
                        }
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
                            if (reader.TokenType == JsonTokenType.PropertyName && reader.GetString() == "worldDepth" && ordering != 0)
                            {
                                reader.Read();
                                temp.depth = reader.GetInt32();
                                count++;
                            }
                            if (count == 4 + Math.Abs(ordering))
                            {
                                if (verbosity > 1) Console.WriteLine("Adding level " + lastIdent + " to dict.");
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
            int minDepth = int.MaxValue;
            int maxDepth = int.MinValue;

            if (verbosity > 1) Console.WriteLine("Calculaing image properties");
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
                if (level.Value.depth < minDepth)
                    minDepth = level.Value.depth;
                if (level.Value.depth > maxDepth)
                    maxDepth = level.Value.depth;
            }
            if (ordering == -1)
                (maxDepth, minDepth) = (minDepth, maxDepth);

            if (imgSize[0] < 1) imgSize[0] = borderRight - borderLeft;
            if (imgSize[1] < 1) imgSize[1] = borderBottom - borderTop;

            GraphicsOptions ops = new();
            ops.AlphaCompositionMode = PixelAlphaCompositionMode.Src;
            if (verbosity > 1) Console.WriteLine("Starting image generation");
            if (verbosity > 1) Console.WriteLine("min depth is " + minDepth + " and max depth is " + maxDepth);
            using (Image<Rgba32> canvas = new(imgSize[0], imgSize[1]))
            {
                for (int d = minDepth;;)
                {
                    if (verbosity > 1 && ordering != 0) Console.WriteLine("Drawing rooms at depth " + d);
                    foreach (KeyValuePair<string, Level> level in levelInfo)
                    {
                        if (level.Value.depth != d && ordering != 0) continue;
                        if (verbosity > 0) Console.WriteLine("Drawing room: " + level.Key);
                        try
                        {
                            using (Image room = Image.Load(imgPath + level.Key + ".png"))
                                canvas.Mutate(x => x.DrawImage(room, new Point(level.Value.x - borderLeft, level.Value.y - borderTop), ops));
                        }
                        catch (Exception ex)
                        {
                            if (verbosity > 1) Console.WriteLine("Could not load room " + imgPath + level.Key +".png:\n" + ex.ToString());
                            else if (verbosity > 0) Console.WriteLine("Could not load room " + imgPath + level.Key + ".png!");
                        }
                    }
                    d += ordering;
                    if ((ordering == 1 && d > maxDepth) || (ordering == -1 && d < maxDepth) || ordering == 0)
                        break;
                }
                if (scaleDenom != 1)
                    canvas.Mutate(x => x.Resize((int)(canvas.Width / scaleDenom), (int)(canvas.Height / scaleDenom), KnownResamplers.NearestNeighbor));
                if (verbosity > 0) Console.WriteLine("Saving the output image");
                canvas.Save("map.png");
            }


            #endregion

            return 0;
        }
    }
}
