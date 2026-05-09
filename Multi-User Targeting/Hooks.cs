using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace MultiUserTargeting
{
    class Hooks
    {
        private static bool hasMath = false;

        internal static void InitializeHooks()
        {
            On.RoR2.NetworkUser.UpdateUserName += Hooks.NetworkUser_UpdateUserName;//subscribe the hooks
            On.RoR2.Console.SubmitCmd_CmdSender_string_bool += Hooks.Console_SubmitCmd_CmdSender;
            On.RoR2.UI.ConsoleWindow.OnInputFieldValueChanged += Hooks.ConsoleWindow_OnInputFieldChanged;
        }

        internal static void DisableHooks()
        {
            On.RoR2.NetworkUser.UpdateUserName -= Hooks.NetworkUser_UpdateUserName;//subscribe the hooks
            On.RoR2.Console.SubmitCmd_CmdSender_string_bool -= Hooks.Console_SubmitCmd_CmdSender;
            On.RoR2.UI.ConsoleWindow.OnInputFieldValueChanged -= Hooks.ConsoleWindow_OnInputFieldChanged;
        }

        /// <summary>
        /// Automatically adds quotation marks.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="s"></param>
        /// <param name="input"></param>
        public static void ConsoleWindow_OnInputFieldChanged(On.RoR2.UI.ConsoleWindow.orig_OnInputFieldValueChanged o, ConsoleWindow s, string input)
        {
            o.Invoke(s, input);
            if (input.ToLower().Contains("all:") && !input.ToLower().Contains("\"all:")
                 || input.ToLower().Contains("me:") && !input.ToLower().Contains("\"me:"))
            {//if a special case M.U.T. id has been typed without an opening quote
                string tmpText = input.ToLower().Replace("all:", "\"All:");//add the open quote
                tmpText = tmpText.ToLower().Replace("me:", "\"Me:");
                ConsoleWindow.instance.inputField.text = tmpText;//update the text visually
                input = input.ToLower().Replace("all:", "\"All:");//add the open quote
                input = input.ToLower().Replace("me:", "\"Me:");//also updates the text internally
                ConsoleWindow.instance.inputField.MoveToEndOfLine(false, false);//move the cartet to the end
            }

            if ((input.ToLower().Contains("all:") || input.ToLower().Contains("me:")) && input.EndsWith(" ") && !input.EndsWith("\" "))
            {//when you press space on an "all:"/"me:"
                if (!hasMath)
                {//only run this once as long as "all:"/"me:" exists
                    hasMath = true;//flag lock
                    ConsoleWindow.instance.inputField.text = input.Insert(input.LastIndexOf(' '), "\"");//add closing quote before space so user can constantly write without worrying about the quotation marks
                    input = input.Insert(input.LastIndexOf(' '), "\"");//update internal text
                    ConsoleWindow.instance.inputField.MoveToEndOfLine(false, false);//move to end of cartet
                }
            }

            if (!input.ToLower().Contains("all:") && !input.ToLower().Contains("me:"))
            {//if "all:"/"me:" does not exist
                hasMath = false;//reset flag
            }
        }

        /// <summary>
        /// Handles checking the validity of the command and arguments being input before it's sent to the M.U.T. system (command sanitisation).
        /// </summary>
        /// <param name="o"></param>
        /// <param name="s"></param>
        /// <param name="sender"></param>
        /// <param name="cmd"></param>
        /// <param name="record"></param>
        public static void Console_SubmitCmd_CmdSender(On.RoR2.Console.orig_SubmitCmd_CmdSender_string_bool o, RoR2.Console s, RoR2.Console.CmdSender sender, string cmd, bool record)
        {
            List<string> cmdSplit = cmd.Split(' ').ToList();
            string mainCommand = cmdSplit[0];
            cmdSplit.RemoveAt(0);

            //----------auto-quotation for arguments, command sanitisation and 'assuming' auto complete  (for M.U.T. only) ----------//
            if (cmdSplit.Count > 0)
            {
                cmdSplit.RemoveAll(e => e == " " || e == "");//remove all empty or dead space entries

                List<int> quotationIndex = new List<int>();
                List<string> operators = MUT.entryPoint.specialOperators.ToList();
                operators.AddRange(MUT.entryPoint.specialMathOperators.ToList());
                List<int> assumingIndex = new List<int>();
                List<int[]> mathIndex = new List<int[]>();
                bool changeMade = false;

                for (int commandIndex = 0; commandIndex < cmdSplit.Count; commandIndex++)
                {//foreach command
                    string command = cmdSplit[commandIndex];
                    if (operators.Any((string e) => (!command.StartsWith("\"") || !command.EndsWith("\"")) && command.Contains(e)))
                    {//if the current command starts or ends with quotation marks then return false otherwise if it contains any symbol in either operators list;
                        quotationIndex.Add(cmdSplit.IndexOf(command));//store the index of the command in the command set
                    }
                    //----------'assuming' auto complete----------//
                    string[] specialOperatorsSplit = command.Split(MUT.entryPoint.specialOperators, StringSplitOptions.RemoveEmptyEntries);
                    for (int specialOperatorsIndex = 0; specialOperatorsIndex < specialOperatorsSplit.Length; specialOperatorsIndex++)
                    {//foreach command split by the specialOperators
                        string commandPart = specialOperatorsSplit[specialOperatorsIndex];
                        mathIndex.Add(new int[MUT.entryPoint.specialMathOperators.Length]);//(0 is default)
                        MUT.entryPoint.specialMathOperators.ToList().ForEach(e => mathIndex[mathIndex.Count - 1][MUT.entryPoint.specialMathOperators.ToList().IndexOf(e)]
                        = (!commandPart.Contains(':')) ? commandPart.IndexOf(e) : -(commandPart.IndexOf(e) + 3));
                        //(get the indicies of all the existing mathOperators that exist in the current command)
                        //foreach mathOperator: if the current command does not have a ':' then of the [just added list] of [each loop of mathOperators];
                        //set the index to whatever the index is of the current mathOperator in the current command (either -1 if none or otherwise >= 0),
                        //otherwise report if there is a ':' AND if there is (<= -3) or is not (-2) a SpecialMathOperator. This translates to:
                        //if mathindex[][] == -1; there is no mathOperator in the command and no collon (i.e. do nothing)
                        //if >= 0; there is a mathOperator in the command and no collon
                        //if == -2; there is no matherOperator but there is a collon
                        //if <= -3; there is a mathOperator and there is a collon
                        if (commandPart.IndexOf(':') != -1 || mathIndex[specialOperatorsIndex].Max() > -1)
                        {//if there is at least a : or a specialOperator
                            if (mathIndex[specialOperatorsIndex].Count(e => e <= -3 || e >= 0) > 1)
                            {//if multiple math operations are detected
                                Trace.TraceWarning("User has input a command which contains multiple math operators in a single query at query line: \"" + commandPart + "\" of argument " + commandIndex
                                    + "! The command will not be sent! To chain math operations together please add a '&' or '&!' after the first query.");
                                MonoBehaviour.print("User has input a command which contains multiple math operators in a single query at query line: \"" + commandPart + "\" of argument " + commandIndex
                                    + "! The command will not be sent! To chain math operations together please add a '&' or '&!' after the first query.");
                                return;
                            }

                            int startIndex = 0;
                            bool wrn = false;
                            for (int mathOperatorIndex = 0; mathOperatorIndex < mathIndex[specialOperatorsIndex].Length; mathOperatorIndex++)
                            {//foreach specialMathOperator of the current search querey
                                int index = mathIndex[specialOperatorsIndex][mathOperatorIndex];
                                if (index > -1)
                                {
                                    Trace.TraceWarning("User has input a command which contains the math operator \"" + MUT.entryPoint.specialMathOperators[mathOperatorIndex]
                                        + "\" with no math identifier (':') at query line: \"" + commandPart + "\" of argument " + commandIndex + "!");
                                    MonoBehaviour.print("User has input a command which contains the math operator \"" + MUT.entryPoint.specialMathOperators[mathOperatorIndex]
                                        + "\" with no math identifier (':') at query line: \"" + commandPart + "\" of argument " + commandIndex + "!");
                                }
                                else if (mathIndex[specialOperatorsIndex].Min() == -2 && !wrn)
                                {
                                    wrn = true;
                                    Trace.TraceWarning("User has input a command which contains a ':' with no math operator at query line: \"" + commandPart + "\" of argument "
                                        + commandIndex + ", expect things to break!");
                                    MonoBehaviour.print("A command which contains a ':' with no math operator is being sent at query line: \"" + commandPart + "\" of argument "
                                        + commandIndex + ", expect things to break!");
                                }
                                else if (index <= -3)
                                {
                                    if (commandPart.IndexOf(':') == -1 || commandPart.IndexOf(':') == 0 || commandPart.IndexOf("\":") == 0)
                                    {//if ':' doesn't exist, is the first character or is the first character after a quotation mark (if there are charcters prior other than a quotation mark then assume that there is data)
                                        index = -index;//invert the index to be possitive
                                        int diffOfIndex = index - startIndex;//index of the current mathOperator - index of the previous mathOperator
                                        int colIndex = commandPart.IndexOf(':', startIndex, diffOfIndex);//find the ':' by searching from the previous mathOperator (or from start if none) to the current

                                        if (colIndex < index && colIndex >= 0)
                                        {//if the found index is not null(-1) and the found index prior to the current mathOperator
                                            if (colIndex > 0)
                                            {
                                                cmdSplit[commandIndex] = cmdSplit[commandIndex].Insert(colIndex, "*");//found the target ':', adding the * proir to it
                                                changeMade = true;
                                            }
                                            else
                                            {
                                                cmdSplit[commandIndex] = cmdSplit[commandIndex].Replace(commandPart, ("*" + commandPart));
                                                changeMade = true;
                                            }
                                            startIndex = index;//store the current mathOperator index for the next loop
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (changeMade)
                {
                    cmd = mainCommand + " ";//untested
                    cmdSplit.ForEach(e => cmd += (e == cmdSplit[0]) ? e : (" " + e));//Re-fill cmd with the extra *:. Same as: cmd = cmdSplit[0] + " " + cmdSplit[1] + ...;
                }

                if (quotationIndex.Count > 0)
                {//if there is at least one detected item with an operation in it
                    quotationIndex.ForEach(e => cmdSplit[e] = (cmdSplit[e].IndexOf("\"") != 0) ? "\"" + cmdSplit[e] + "\"" : cmdSplit[e] + "\"");//add the end and start quotation marks to the item
                    cmd = mainCommand + " ";//reset cmd so it can be refilled
                    cmdSplit.ForEach(e => cmd += (e == cmdSplit[0]) ? e : (" " + e));//Re-fill cmd with the extra quote marks. Same as: cmd = cmdSplit[0] + " " + cmdSplit[1] + ...;
                }
            }
            o.Invoke(s, sender, cmd, record);
        }

        /// <summary>
        /// Username sanitisation for M.U.T.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="s"></param>
        public static void NetworkUser_UpdateUserName(On.RoR2.NetworkUser.orig_UpdateUserName o, NetworkUser s)
        {
            string username = s.userName;
            if (username.Length == 0)
            {//if the name is empty
                o.Invoke(s);//let the system get the name
            }
            username = s.userName;//update name
            MUT.entryPoint.unsanitisedName = username;//store unsanitised name for tracking purposes

            //string dbgName = "uncomment this codepart and put a test name here to debug username sanitisation";
            //if (username != dbgName)
            //{
            //    username = dbgName;
            //    s.userName = dbgName;
            //}

            while (MUT.entryPoint.sanitisationSymbols.Any(s.userName.Contains))
            {//while the username contains any strings in sanitisationSymbols (this should only need to run once per name)
                foreach (string symbol in MUT.entryPoint.sanitisationSymbols)
                {//for every symbol;
                    if (username.IndexOf(symbol) != -1)
                    {//if the name has the symbol
                        s.userName = s.userName.Remove(username.IndexOf(symbol), symbol.Length);//sanitise the name of the symbol
                    }
                }
            }

            if (s.userName == "")
            {
                string ID = s.id.steamId.value.GetHashCode().ToString();//get the user's steamID and make the number a hash (so that it's easier to target)
                s.userName = "U" + ID;
            }
        }

    }
}
