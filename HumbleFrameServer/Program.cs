#if DEBUG
#define NOBUBBLE
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HumbleFrameServer.Lib;
using HumbleFrameServer.Base;
using System.IO;
using System.Reflection;
using System.Configuration;
using System.Collections.Specialized;
using System.Drawing;
using System.Diagnostics;
using System.Resources;
using System.Globalization;
using System.Threading;


namespace HumbleFrameServer
{
    internal class FunctionDefinition
    {
        //public Assembly ContainerAssembly { get; set; }
        public Type InstanceType { get; set; }
        public NodeType FunctionType { get; set; }

        public object GetNewInstance()
        {
            return Activator.CreateInstance(this.InstanceType);
        }
    }

    internal class StreamsTree
    {
        public string FunctionName { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public iAudioVideoStream AudioVideoStreamInstance { get; set; }

        public StreamsTree()
        {
            FunctionName = string.Empty;
            Parameters = new Dictionary<string, object>();
        }
    }

    internal class Humfs
    {
        private bool _showFunctionsList = false;
        private string _scriptPath = "";
        private string _outputPath = "";
        private bool _stdoutOutput = false;
        private iAudioVideoStream _render = null;

        public Humfs(string[] args)
        {


#if BUBBLE
            try
#endif
            {
                Assembly current = Assembly.GetExecutingAssembly();
                logger(string.Format(TextRes.app_name_version,
                        current.GetName().Version.ToString(3),
                        current.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion,
                        current.GetCustomAttribute<AssemblyBuildDateAttribute>().BuildDate
                    )
                );
                parseArgs(args);
                loadPlugIns();
                if (!_showFunctionsList)
                {
                    parseScript();
                    Begin();
                }
            }
#if BUBBLE
            catch (Exception eX)
            {
                logger(eX);
            }
#endif
        }

        #region Logger

        private void logger(string Message)
        {
            Console.Error.WriteLine(Message);
        }

        private void logger(Exception eX)
        {
            logger(eX.Message);
        }

        #endregion

        private void parseArgs(string[] args)
        {
            /* -i filename: input .hum script
             * -o filename: output uncompressed .avi file
             * -o -: output to stdout
             * -o null: no output
             * -debugit: force it-IT culture
             * -fl: functions list
             * */
            List<string> outputText = new List<string>();

            if (args.Count(x => x == "-debugit") > 0)
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
            }

            if (args.Count(x => x == "-fl") > 0)
            {
                _showFunctionsList = true;
            }
            else
            {
                for (int a = 0; a < args.Length; a++)
                {
                    switch (args[a])
                    {
                        case "-i":
                            a++;
                            _scriptPath = args[a];
                            outputText.Add(string.Format(TextRes.param_input, _scriptPath));
                            break;
                        case "-o":
                            a++;
                            _outputPath = args[a];
                            _stdoutOutput = _outputPath == "-";
                            outputText.Add(string.Format(TextRes.param_output, (!_stdoutOutput ? "file://" + _outputPath : "stdout")));
                            break;
                    }
                }
            }

            if (!_showFunctionsList && (_scriptPath == "" || _outputPath == ""))
            {
                throw new ArgumentException(TextRes.usage);
            }
            else if (!_stdoutOutput)
            {
                foreach (string t in outputText)
                {
                    logger(t);
                }
            }
        }

        //TODO: usare un oggetto per tenere traccia dell'utilizzo della variabile e dare messaggi di errore più utili
        private Dictionary<string, object> _Variables;

        private CultureInfo _US_Culture = new CultureInfo("en-US");
        private NodeParameter parseRow(string textRow,/* out bool FunctionAsVariable, */iAudioVideoStream Function = null)
        {
            iAudioVideoStream newFunction = Function;
            string newFunctionName = newFunction != null ? newFunction.NodeName : string.Empty;

            textRow = textRow.Trim();
            bool isString = false;
            int beginOfElement = -1;
            string elementText = "";
            string currentParam = "";
            //bool isVariable = false;
            bool isVariableAssigned = false;
            string currentVar = "";
            bool isLineDialog = false;
            bool FunctionAsVariable = false;

            for (int c = 0; c < textRow.Length; c++)
            {
                //End of function name / begin of parameters list
                if (textRow[c] == '(')
                {
                    //Function name
                    if (newFunctionName == string.Empty)
                    {
                        newFunctionName = textRow.Substring(beginOfElement, c - beginOfElement).ToLower();
                        if (!_functionsCatalog.ContainsKey(newFunctionName))
                        {
                            throw new Exception(string.Format(TextRes.unknown_function, newFunctionName));
                            //break;
                        }
                        else
                        {
                            switch (_functionsCatalog[newFunctionName].FunctionType)
                            {
                                case NodeType.Input:
                                case NodeType.Filter:
                                    newFunction = (iAudioVideoStream)_functionsCatalog[newFunctionName].GetNewInstance();
                                    break;
                                default:
                                    throw new Exception(string.Format(TextRes.unknown_function_type, _functionsCatalog[newFunctionName].FunctionType));
                                    //break;
                            }
                        }
                    }

                    //Function as parameter
                    else
                    {
                        //Find the corresponding closed bracket
                        int bracketCount = 1;
                        for (c++; c < textRow.Length; c++)
                        {
                            if (textRow[c] == ')')
                                bracketCount--;
                            if (textRow[c] == '(')
                                bracketCount++;

                            if (bracketCount == 0)
                            {
                                c++;
                                if (currentParam == "" && currentVar != "")
                                {
                                    if (_Variables.Count(x => x.Key == currentVar) == 0)
                                    {
                                        _Variables.Add(currentVar, parseRow(textRow.Substring(beginOfElement, c - beginOfElement)));
                                    }
                                    else
                                    {
                                        throw new Exception(string.Format("Variable {0} already defined", currentVar));
                                    }
                                }
                                else if (currentParam != "")
                                {
                                    if (newFunction.Parameters.Count(x => x.Key == currentParam) == 1)
                                    {
                                        newFunction.Parameters[currentParam].Value = parseRow(textRow.Substring(beginOfElement, c - beginOfElement));
                                    }
                                    else
                                    {
                                        throw new Exception(string.Format("{0}: Unknown parameter \"{1}\"", newFunction.NodeName, currentParam));
                                    }
                                }
                                else
                                {
                                    newFunction.Parameters.Last().Value.Value = parseRow(textRow.Substring(beginOfElement, c - beginOfElement));
                                }
                                break;
                            }
                        }
                    }
                    beginOfElement = -1;
                    elementText = "";
                }

                //End of the parameter/variable name - begin of parameter/variable value
                else if (textRow[c] == '=')
                {
                    if (elementText[0] == '@')
                    {
                        currentVar = elementText;
                        //isVariable = false;
                    }
                    else
                    {
                        currentParam = elementText;
                    }

                    beginOfElement = -1;
                    elementText = "";
                }

                //End of parameter value / begin of new parameter
                else if (textRow[c] == ',' || textRow[c] == ')' || textRow[c] == ';')
                {
                    if (currentParam != "")
                    { //Parameter
                        if (newFunction.Parameters.Count(x => x.Key == currentParam) != 1)
                        {
                            throw new Exception(string.Format("{0}: Unknown parameter \"{1}\"", newFunction.NodeName, currentParam));
                        }

                        if (elementText.Length > 0 && elementText[0] == '@')
                        {
                            if (!_Variables.ContainsKey(elementText))
                            {
                                throw new Exception(string.Format("Unknown variable \"{0}\"", elementText));
                            }
                            if (newFunction.Parameters[currentParam].Type != NodeParameterType.AudioVideoStream)
                            {
                                elementText = _Variables[elementText] as string;
                            }
                        }

                        switch (newFunction.Parameters[currentParam].Type)
                        {
                            case NodeParameterType.String:
                                newFunction.Parameters[currentParam].Value = elementText;
                                break;

                            case NodeParameterType.Int:
                                int parsedInt = 0;
                                if (int.TryParse(elementText, out parsedInt))
                                    newFunction.Parameters[currentParam].Value = parsedInt;
                                else
                                    throw new Exception(string.Format("{0}|{1}: value must be integer", newFunction.NodeName, currentParam));
                                break;

                            case NodeParameterType.Decimal:
                                decimal parsedDecimal = 0m;
                                if (decimal.TryParse(elementText, NumberStyles.Float, _US_Culture, out parsedDecimal))
                                    newFunction.Parameters[currentParam].Value = parsedDecimal;
                                else
                                    throw new Exception(string.Format("{0}|{1}: value must be decimal", newFunction.NodeName, currentParam));
                                break;

                            case NodeParameterType.Bool:
                                bool parsedBool = false;
                                if (bool.TryParse(elementText, out parsedBool))
                                    newFunction.Parameters[currentParam].Value = parsedBool;
                                else
                                    throw new Exception(string.Format("{0}|{1}: value must be true or false", newFunction.NodeName, currentParam));
                                break;

                            case NodeParameterType.AudioVideoStream:
                                //Succede solo se sono variabili?
                                if (elementText != "")
                                {
                                    newFunction.Parameters[currentParam].Value = _Variables[elementText] as iAudioVideoStream;
                                    _Variables.Remove(elementText);
                                }
                                //isVariable = false;
                                break;
                            default:
                                throw new Exception(string.Format(TextRes.unknown_param_type, newFunction.Parameters[currentParam].Type));
                                //break;
                        }
                        currentParam = "";
                    }

                    if (textRow[c] == ';' && (currentVar != "" || (elementText.Length > 0 && elementText[0] == '@')))
                    {
                        //Variable
                        if (_Variables.Count(x => x.Key == currentVar) > 0)
                        {
                            throw new Exception(string.Format("Variable {0} already defined", currentVar));
                        }

                        if (elementText.Length > 0 && elementText[0] == '@')
                        {
                            newFunction = _Variables[elementText] as iAudioVideoStream;
                            isVariableAssigned = false;
                        }
                        else if (newFunction != null)
                        {
                            _Variables.Add(currentVar, newFunction);
                            isVariableAssigned = false;
                            FunctionAsVariable = true;
                        }
                        else
                        {
                            _Variables.Add(currentVar, elementText);
                            isVariableAssigned = true;
                        }


                        break;
                    }

                    beginOfElement = -1;
                    elementText = "";
                }

                //Begin or end of a string
                else if (textRow[c] == '"' && textRow[c - 1] != '"')
                {
                    //TODO: support per escape '\'. Per esempio questo "Testo \"tra virgolette\"" non funzionerebbe
                    isString = !isString;
                }

                //Comment
                else if (!isString && textRow[c] == '#')
                {
                    isLineDialog = true;
                    break;
                }

                //Normal char
                else
                {
                    //Skips blank if not string
                    if ((isString || textRow[c] != ' ') && !isLineDialog)
                    {
                        if (beginOfElement < 0)
                            beginOfElement = c;

                        elementText += textRow[c];
                    }
                }
            }

            return isVariableAssigned ? null : new NodeParameter()
            {
                AsVariable = FunctionAsVariable,
                Type = NodeParameterType.AudioVideoStream,
                Value = newFunction
            };
        }

        /// <summary>
        /// Parses the input script and composes the streams tree
        /// </summary>
        /// <returns>true if successful</returns>
        private void parseScript()
        {
            int rowCount = 0;

            _Variables = new Dictionary<string, object>();
            TextReader inScript = File.OpenText(_scriptPath);
            _render = new Renderer();

            try
            {
                string textLine = inScript.ReadLine();
                while (textLine != null)
                {
                    textLine = textLine.Trim();
                    // # is a comment
                    if (textLine.Length > 0 && textLine[0] != '#')
                    {
                        NodeParameter newPar = parseRow(textLine, null /*_render*/ );
                        _render.Parameters.Add(rowCount.ToString(), newPar);
                    }

                    textLine = inScript.ReadLine();
                    rowCount++;
                }

                inScript.Close();
            }
            catch (Exception eX)
            {
                throw new Exception(string.Format("Error in script: line {0} - {1}", rowCount + 1, eX.Message));
            }
        }

        /// <summary>
        /// Catalog of available functions (audiovideostream and scalar) as read from plugins
        /// </summary>
        private Dictionary<string, FunctionDefinition> _functionsCatalog;

        /// <summary>
        /// Scans configured folders for plugins and compiles the functions catalog
        /// </summary>
        /// <returns></returns>
        private void loadPlugIns()
        {
            StringBuilder outputText = new StringBuilder("\r\nFunctions list:\r\n");

            string[] blackList = {
                "avcodec-58.dll",
                "avdevice-58.dll",
                "avfilter-7.dll",
                "avformat-58.dll",
                "avutil-56.dll",
                "postproc-55.dll",
                "swresample-3.dll",
                "swscale-5.dll",
                "FFMPEG_Cli.dll"
            };

            try
            {
                Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

                _functionsCatalog = new Dictionary<string, FunctionDefinition>();

                string[] pluginPaths = ConfigurationManager.AppSettings["pluginspath"].Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string pluginPath in pluginPaths)
                {
                    string[] pluginFiles = Directory.GetFiles(pluginPath, "*.dll", SearchOption.TopDirectoryOnly).Where(x =>
                    !blackList.Any(y => y == Path.GetFileName(x))).ToArray();
                    foreach (string pluginFile in pluginFiles)
                    {
                        Assembly plugin = Assembly.LoadFrom(pluginFile);
                        foreach (Type t in plugin.ExportedTypes)
                        {
                            if (t.GetInterfaces().Contains(typeof(iAudioVideoStream)))
                            {
                                iAudioVideoStream tempPlugin = (iAudioVideoStream)Activator.CreateInstance(t);
                                _functionsCatalog.Add(tempPlugin.NodeName.ToLower(), new FunctionDefinition()
                                {
                                    FunctionType = tempPlugin.Type,
                                    InstanceType = t
                                });
                            }
                        }
                    }
                }

                if (_showFunctionsList)
                {
                    foreach (FunctionDefinition f in _functionsCatalog.Values)
                    {
                        switch (f.FunctionType)
                        {
                            case NodeType.Input:
                            case NodeType.Filter:
                                iAudioVideoStream tempFunction = f.GetNewInstance() as iAudioVideoStream;

                                outputText.AppendFormat("\r\n{0} - type: {1}\r\n", tempFunction.NodeName, tempFunction.Type);
                                outputText.AppendFormat("    {0}\r\n", tempFunction.NodeDescription);
                                if (tempFunction.Parameters.Count > 0)
                                {
                                    outputText.Append("    Parameters:\r\n");
                                    foreach (string p in tempFunction.Parameters.Keys)
                                    {
                                        outputText.AppendFormat("        {0} ({1})\r\n", p, tempFunction.Parameters[p].Type);
                                    }
                                }
                                else
                                {
                                    outputText.Append("    Parameters: none\r\n");
                                }
                                break;
                        }
                    }

                    logger(outputText.ToString());
                }
            }
            catch (Exception eX)
            {
                throw new Exception("Error loading plugins.");
            }
        }

        private void Begin()
        {
            _render.openStream();
            OutputStream outFile;

            switch (_outputPath)
            {
                case "-":
                    outFile = new OutputStream(Console.OpenStandardOutput(), _render.hasVideo, _render.hasAudio);
                    break;
                case "null":
                    outFile = new OutputStream(new NullStream(), _render.hasVideo, _render.hasAudio);
                    break;
                default:
                    outFile = new OutputStream(File.Create(_outputPath), _render.hasVideo, _render.hasAudio);
                    break;
            }

            outFile.initStream(_render.FPS, _render.Width, _render.Height, _render.SamplesPerSecond, _render.ChannelsCount, _render.BitsPerSample);

            DataPacket currentPacket;

            Stopwatch sw = new Stopwatch();
            sw.Start();
            while ((currentPacket = _render.getNextPacket()) != null && currentPacket.Data != null)
            {
                switch (currentPacket.Type)
                {
                    case PacketType.Audio:
                        outFile.WriteAudioSample((int[])currentPacket.Data);
                        break;
                    case PacketType.Video:
                        outFile.WriteVideoFrame((Bitmap)currentPacket.Data);
                        break;
                    default:
                        throw new NotImplementedException();
                        //break;
                }
            }

            sw.Stop();
            Console.WriteLine("Elapsed time: {0}", sw.Elapsed);

            outFile.Close();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Humfs _humfs = new Humfs(args);
            if (Debugger.IsAttached)
            {
                Console.WriteLine("Press any key");
                Console.ReadKey();
            }
        }
    }
}
