using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BepInEx;
using PlayerStatsAPI;
using RoR2;
using UnityEngine;

namespace MultiUserTargeting
{
    [BepInPlugin("com.ML.MUT", "Console Overhaul: Multi-User Targeting", "1.0")]
    [BepInDependency(TMIDep)]

    public class MUT : BaseUnityPlugin
    {
        private const string TMIDep = "com.ML.PlayerStatsAPI";
        /// <summary>
        /// Stores all of the non-mathmaticall (main) operators the multi-targeting system expects to see when quereying for multiple CharacterBodies
        /// </summary>
        public readonly string[] specialOperators = { "&!", "&" };

        /// <summary>
        /// Stores all of the mathmaticall inciting operators the multi-targeting system expects to see when quereying for or against multiple CharacterBodies
        /// </summary>
        public readonly string[] specialMathOperators = { "=", ">", "<", ">=", "<=", "!=" };

        internal string[] sanitisationSymbols = { ":", "me", "*", "all", "alive", "spectator", "!", "-", "local", "\"", "'" };

        /// <summary>
        /// Stores all of the CharacterBodies found from the previous CC's querey. (should really stowe this into a temp var and clear this after all bodies have been found)
        /// </summary>
        private List<CharacterBody> characterBodies = new List<CharacterBody>();

        /// <summary>
        /// Unsanitised name of the current client.
        /// </summary>
        public string unsanitisedName = "";

        /// <summary>
        /// Entry point used to access non-static methods.
        /// </summary>
        public static MUT entryPoint = new MUT();

        private MUT()
        {//construct the sanitisation symbols list
            List<string> symbols = sanitisationSymbols.ToList();//main operator symbols
            symbols.AddRange(specialOperators);
            symbols.AddRange(specialMathOperators);
            sanitisationSymbols = symbols.ToArray();
        }

        public void Awake()
        {
            Hooks.InitializeHooks();
        }

        public void OnDisable()
        {
            Hooks.DisableHooks();//drop hooks
        }

        /// <summary>
        /// Returns the CharacterBody of the player with the matching name. If none are found returns null.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public CharacterBody GetPlayerBodyByName(string name)
        {
            if (name.ToUpper().Equals("ME"))
            {//if the name IS just 'me'
                return LocalUserManager.GetFirstLocalUser().cachedBody;//return the local player
            }

            if (name.Length > 2)
            {//if the name isn't just "me"
                if (name.ToUpper().StartsWith("ME"))
                {//but starts with "me"
                    //check if the name contains a search pattern (a specialOperator)
                    bool containsSO = false;
                    foreach (string SO in specialOperators)
                    {
                        if (name.Contains(SO))
                        {
                            containsSO = true;
                            break;
                        }
                    }
                    if (containsSO)
                    {//if the name does, warn the modder and return nothing.
                        Trace.TraceError("refr.GetPlayerBodyByName: A call to GPBBN was made where the name contained a special operator." +
                            " If this was called from a console command, please update your command ASAP to use 'GetPlayerBodiesByName' instead," +
                            " or add a checker for single target only using an if statemeny with the CheckIfSingleTarget() method.");
                        return null;
                    }
                }
            }
            foreach (NetworkUser user in NetworkUser.readOnlyInstancesList)
            {//otherwise we're looking for a player, for each user
                if (user.userName.Equals(name, StringComparison.InvariantCultureIgnoreCase) && user.master.GetBody().isPlayerControlled)
                {//if the user's username matches the search querey and is a player
                    return user.GetCurrentBody();//return the players' characterBody
                }
            }
            return null;//if there are no players or the player could not be found then return nothing.
        }

        /// <summary>
        /// Returns the list of all the CharacterBodies which matched the search query defined in the name and weather to add or remove the matched CharacterBodies from the local characterBodies list.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="AddOrRemove"></param>
        /// <returns></returns>
        public List<CharacterBody> GetPlayerBodiesByName(string name, bool AddOrRemove = false)
        {
            if (name.Contains("*"))
            {
                name = name.Replace("*", "ALL");
            }

            characterBodies.Clear();
            if (name.ToUpper().StartsWith("ME"))
            {                                                                                                                                               //Tif name is "ME" operator
                bool isPlayerName = false;                                                                                                                  //|-- prepare to check if a name or the "ME" operator
                if (name.Length > 2)                                                                                                                        //|
                {                                                                                                                                           //|-T if the string not just "ME"
                    if (Char.IsLetter(name.ToUpper().ToCharArray()[2]))                                                                                     //| |
                    {                                                                                                                                       //|-|--T check if the next character after the opperator an english letter
                        isPlayerName = true;                                                                                                                //|-|--|---- the string is a name, not a "ME" operator
                    }                                                                                                                                       //|-|--\e
                }                                                                                                                                           //|-\e
                                                                                                                                                            //|
                if (!isPlayerName)                                                                                                                          //|
                {                                                                                                                                           //|-T if the string is a "ME" operator
                    if (name.ToUpper().StartsWith("ME:"))                                                                                                   //| |
                    {                                                                                                                                       //|-|--T if the string is a math "ME" operator
                        List<CharacterBody> characterBodiesTMP = new List<CharacterBody> {                                                                  //|-|--|---T create a new temporary character bodies list
                            LocalUserManager.GetFirstLocalUser().cachedBody                                                                                 //|-|--|---|----- fill with one entry of the local players body
                        };                                                                                                                                  //|-|--|---\List
                                                                                                                                                            //| |  |
                        if (!AddOrRemove && !characterBodies.Contains(LocalUserManager.GetFirstLocalUser().cachedBody))                                     //| |  |
                        {                                                                                                                                   //|-|--|---T if not removing and the local players body does not already exist in the main list
                            characterBodies.AddRange(handleSpecialMath(name, characterBodiesTMP));                                                          //|-|--|---|----- add the bodies that matched the math operation to the main list
                        }                                                                                                                                   //| |  |   |
                        else                                                                                                                                //|-|--|---\c otherwise if removing and the body does exist in the main list
                        {                                                                                                                                   //| |  |   |
                            foreach (CharacterBody body in handleSpecialMath(name, characterBodiesTMP))                                                     //| |  |   |
                            {                                                                                                                               //|-|--|---|----T for each body returned by the math operation
                                characterBodies.Remove(body);                                                                                               //|-|--|---|----|------- remove the bodies that matched the math operation from the main list
                            }                                                                                                                               //|-|--|---|----\L
                        }                                                                                                                                   //|-|--|---\e
                    }                                                                                                                                       //| |  |
                    else                                                                                                                                    //|-|--\c if the string is a normal "ME" operator
                    {                                                                                                                                       //| |  |
                        if (!AddOrRemove && !characterBodies.Contains(LocalUserManager.GetFirstLocalUser().cachedBody))                                     //| |  |
                        {                                                                                                                                   //|-|--|---T if not removing and the player's body is not already in the list
                            characterBodies.Add(LocalUserManager.GetFirstLocalUser().cachedBody);                                                           //|-|--|---|----- add the local player's body to the list
                        }                                                                                                                                   //| |  |   |
                        else                                                                                                                                //|-|--|---\c otherwise if removing AND the body is contained in the list
                        {                                                                                                                                   //| |  |   |
                            characterBodies.Remove(LocalUserManager.GetFirstLocalUser().cachedBody);                                                        //|-|--|---|----- remove the body from the list
                        }                                                                                                                                   //|-|--|---\e
                    }                                                                                                                                       //|-|--\e
                }                                                                                                                                           //|-\e
            }                                                                                                                                               //|
            else if (name.ToUpper().StartsWith("ALL"))                                                                                                      //\c otherwise if the name is a "ALL" operator
            {                                                                                                                                               //|
                bool isPlayerName = false;                                                                                                                  //|-- prepare to check if a name or the "ALL" operator
                if (name.Length > 3)                                                                                                                        //|
                {                                                                                                                                           //|-T if the string is not just "ALL"
                    if (Char.IsLetter(name.ToUpper().ToCharArray()[3]))                                                                                     //| |
                    {                                                                                                                                       //|-|--T if the next character after the opperator is an english letter
                        isPlayerName = true;                                                                                                                //|-|--| the string is a name, not a "ALL" operator
                    }                                                                                                                                       //|-|--\e
                }                                                                                                                                           //|-\e
                                                                                                                                                            //|
                if (!isPlayerName)                                                                                                                          //|
                {                                                                                                                                           //|-T if the string is a "ALL" operator
                    if (name.ToUpper().StartsWith("ALL:"))                                                                                                  //| |
                    {                                                                                                                                       //|-|--T if the string is a math "ALL" operator
                        List<CharacterBody> characterBodiesTMP = new List<CharacterBody>();                                                                 //|-|--|---- prepare to get all players for sorting
                        foreach (NetworkUser user in NetworkUser.readOnlyInstancesList)                                                                     //| |  |
                        {                                                                                                                                   //|-|--|---T (foreach) get each user
                            if (!AddOrRemove && !characterBodies.Contains(user.master.GetBody()))                                                           //| |  |   |
                            {                                                                                                                               //|-|--|---|----T if adding and the character is not already in the main list
                                characterBodiesTMP.Add(user.master.GetBody());                                                                              //|-|--|---|----|------ get each user's body and add them to a temporary list
                            }                                                                                                                               //| |  |   |    |
                            else if (AddOrRemove)                                                                                                           //| |  |   |    |
                            {                                                                                                                               //|-|--|---|----\c if removing
                                characterBodiesTMP.Add(user.master.GetBody());                                                                              //|-|--|---|----|------ get each user's body and add them to a temporary list to be removed from the main list later
                            }                                                                                                                               //|-|--|---|----\e
                        }                                                                                                                                   //|-|--|---\L
                                                                                                                                                            //| |  |
                        if (!AddOrRemove)                                                                                                                   //| |  |
                        {                                                                                                                                   //|-|--|---T if not removing
                            characterBodies.AddRange(handleSpecialMath(name, characterBodiesTMP));                                                          //|-|--|---|----- add the bodies that matched the math operation
                        }                                                                                                                                   //| |  |   |
                        else                                                                                                                                //| |  |   |
                        {                                                                                                                                   //|-|--|---\c if removing
                            foreach (CharacterBody body in handleSpecialMath(name, characterBodiesTMP))                                                     //| |  |   |
                            {                                                                                                                               //|-|--|---|----T for each body returned by the math operation
                                characterBodies.Remove(body);                                                                                               //|-|--|---|----|------ remove the bodies that matched the math operation
                            }                                                                                                                               //|-|--|---|----\L
                        }                                                                                                                                   //|-|--|---\e
                    }                                                                                                                                       //| |  |   
                    else                                                                                                                                    //|-|--\c otherwise if normal "ALL" operator
                    {                                                                                                                                       //| |  |
                        foreach (NetworkUser user in NetworkUser.readOnlyInstancesList)                                                                     //| |  |
                        {                                                                                                                                   //|-|--|---T (foreach) get each user
                            if (!AddOrRemove && !characterBodies.Contains(user.master.GetBody()))                                                           //| |  |   |
                            {                                                                                                                               //|-|--|---|----T if adding and the character is not already in the main list
                                characterBodies.Add(user.master.GetBody());                                                                                 //|-|--|---|----|------ get each user's body and add them to the main list
                            }                                                                                                                               //| |  |   |    |
                            else if (AddOrRemove)                                                                                                           //|-|--|---|----\c otherwise if removing
                            {                                                                                                                               //| |  |   |    |
                                characterBodies.Remove(user.master.GetBody());                                                                              //|-|--|---|----|------ get each user's body and add them to the main list
                            }                                                                                                                               //|-|--|---|----\e
                        }                                                                                                                                   //|-|--|---\L
                    }                                                                                                                                       //|-|--\e
                }                                                                                                                                           //|-\e
            }                                                                                                                                               //|
            else if (name.ToUpper().StartsWith("LOCAL"))                                                                                                    //\c otherwise if the string is a "LOCAL" operator [for splitscreen (you can also use NetworkUser.readOnlyLocalPlayersList) - same as "ME" if playing non-splitscreen]
            {                                                                                                                                               //|
                bool isPlayerName = false;                                                                                                                  //|-- prepare to check if a name or the "LOCAL" operator
                if (name.Length > 5)                                                                                                                        //|
                {                                                                                                                                           //|-T if the string is not just "LOCAL"
                    if (Char.IsLetter(name.ToUpper().ToCharArray()[5]))                                                                                     //| |
                    {                                                                                                                                       //|-|--T if the next character after the opperator an english letter
                        isPlayerName = true;                                                                                                                //|-|--| the string is a name, not a "LOCAL" operator
                    }                                                                                                                                       //|-|--\e
                }                                                                                                                                           //|-\e
                                                                                                                                                            //|
                if (!isPlayerName)                                                                                                                          //|
                {                                                                                                                                           //|-T if the string is a "LOCAL" operator
                    if (name.ToUpper().StartsWith("LOCAL:"))                                                                                                //| |
                    {                                                                                                                                       //|-|--T if the string is a math "LOCAL" operator
                        List<CharacterBody> characterBodiesTMP = new List<CharacterBody>();                                                                 //|-|--|---- prepare to get all players for sorting
                        foreach (NetworkUser user in NetworkUser.readOnlyInstancesList)                                                                     //| |  |
                        {                                                                                                                                   //|-|--|---T (foreach) get each user
                            if (!AddOrRemove && !characterBodies.Contains(user.master.GetBody()))                                                           //| |  |   |
                            {                                                                                                                               //|-|--|---|----T if adding and the character is not already in the main list
                                characterBodiesTMP.Add(user.master.GetBody());                                                                              //|-|--|---|----|------ get each user's body and add them to a temporary list
                            }                                                                                                                               //| |  |   |    |
                            else if (AddOrRemove)                                                                                                           //|-|--|---|----\c if removing
                            {                                                                                                                               //| |  |   |    |
                                characterBodiesTMP.Add(user.master.GetBody());                                                                              //|-|--|---|----|------ get each user's body and add them to a temporary list
                            }                                                                                                                               //|-|--|---|----\e
                        }                                                                                                                                   //|-|--|---\L
                                                                                                                                                            //| |  |
                        if (!AddOrRemove)                                                                                                                   //| |  |
                        {                                                                                                                                   //|-|--|---T if not removing
                            characterBodies.AddRange(handleSpecialMath(name, characterBodiesTMP));                                                          //|-|--|---|----- add the bodies that matched the math operation
                        }                                                                                                                                   //| |  |   |
                        else                                                                                                                                //|-|--|---\c if removing
                        {                                                                                                                                   //| |  |   |
                            foreach (CharacterBody body in handleSpecialMath(name, characterBodiesTMP))                                                     //| |  |   |
                            {                                                                                                                               //|-|--|---|----T for each body returned by the math operation
                                characterBodies.Remove(body);                                                                                               //|-|--|---|----|------ remove the bodies that matched the math operation
                            }                                                                                                                               //|-|--|---|----\L
                        }                                                                                                                                   //|-|--|---\e
                    }                                                                                                                                       //| |  |
                    else                                                                                                                                    //|-|--\c otherwise if normal "LOCAL" operator
                    {                                                                                                                                       //| |  |
                        ReadOnlyCollection<TeamComponent> teamMembers = TeamComponent.GetTeamMembers(TeamIndex.Player);                                     //|-|--|---- get each local player
                        for (int i = 0; i < teamMembers.Count; i++)                                                                                         //| |  |
                        {                                                                                                                                   //|-|--|---T forloop of max count equal to the amount of local players
                            if (!AddOrRemove && !characterBodies.Contains(Util.LookUpBodyNetworkUser(teamMembers[i].gameObject).GetCurrentBody()))          //| |  |   |
                            {                                                                                                                               //|-|--|---|----T if not removing and the mail list doesn't already contain the body
                                characterBodies.Add(Util.LookUpBodyNetworkUser(teamMembers[i].gameObject).GetCurrentBody());                                //|-|--|---|----|------ add the player's body to the main list
                            }                                                                                                                               //| |  |   |    |
                            else if (AddOrRemove)                                                                                                           //|-|--|---|----\c otherwise if removing
                            {                                                                                                                               //| |  |   |    |
                                characterBodies.Remove(Util.LookUpBodyNetworkUser(teamMembers[i].gameObject).GetCurrentBody());                             //|-|--|---|----|------ remove the player's body from the main list
                            }                                                                                                                               //|-|--|---|----|e
                        }                                                                                                                                   //|-|--|---\L
                    }                                                                                                                                       //|-|--\e
                }                                                                                                                                           //|-\e
            }                                                                                                                                               //|
            else                                                                                                                                            //\c
            {                                                                                                                                               //| [find left most specialOperator that exists in the name (if any) and remove all from there]
                List<int> opPoss = new List<int>();                                                                                                         //|-- prepare to get the left most specialOperator
                foreach (string op in specialOperators)                                                                                                     //|
                {                                                                                                                                           //|-T foreach specialOperator
                    opPoss.Add(name.IndexOf(op));                                                                                                           //|-|--- get the current possition the specialOperator appears in the name
                }                                                                                                                                           //|-\L
                                                                                                                                                            //|  [get character which matches name]                                                                                                                                //|-|\e
                string noOp = name;                                                                                                                         //|- get the string of the full name as a temp variable
                if (opPoss.Min() != -1)                                                                                                                     //|
                {                                                                                                                                           //|-T if at least one & or &! has been found
                    noOp = name.Remove(opPoss.Min());                                                                                                       //|-|--- remove any special operators (except math) from the temp variable
                }                                                                                                                                           //|-\e
                                                                                                                                                            //|
                if (noOp.IndexOf(':') != -1)                                                                                                                //|
                {                                                                                                                                           //|-T if the oporator is a math operator [if there is a ':' (note: player's user names' are sanitised) then we are working with a 'target player math operator']
                    List<CharacterBody> characterBodiesTMP = new List<CharacterBody> { GetPlayerBodyByName(noOp.Remove(name.IndexOf(':'))) };               //|-|--- get the body as a List
                    if (characterBodiesTMP[0])                                                                                                              //|-|
                    {                                                                                                                                       //|-|--T if character found
                        if (!AddOrRemove)                                                                                                                   //| |  |
                        {                                                                                                                                   //|-|--|---T if not removing
                            characterBodies.AddRange(handleSpecialMath(name, characterBodiesTMP));                                                          //|-|--|---|---- add the body if it matches the math oporation comparision
                        }                                                                                                                                   //| |  |   |
                        else                                                                                                                                //|-|--|---\c otherwise if removing
                        {                                                                                                                                   //| |  |   |
                            foreach (CharacterBody mathBody in handleSpecialMath(name, characterBodiesTMP))                                                 //| |  |   |
                            {                                                                                                                               //|-|--|---|---T foreach body retruned by the math oporator [will only loop once or none]
                                characterBodies.Remove(mathBody);                                                                                           //|-|--|---|---|----- remove the body from the main list
                            }                                                                                                                               //|-|--|---|---\L
                        }                                                                                                                                   //|-|--|---\e
                    }                                                                                                                                       //| |  \e
                }                                                                                                                                           //| |
                else                                                                                                                                        //| |
                {                                                                                                                                           //|-\c otherwise if not a math oporation
                    CharacterBody body = GetPlayerBodyByName(noOp);                                                                                         //|-|
                    if (body)                                                                                                                               //| |
                    {                                                                                                                                       //|-|--T
                        if (!AddOrRemove && !characterBodies.Contains(body))                                                                                //| |  |
                        {                                                                                                                                   //|-|--|---T if not removing and the main list doesn't contain the body already
                            characterBodies.Add(body);                                                                                                      //|-|--|---|---- add it to the main list
                        }                                                                                                                                   //| |  |   |
                        else if (AddOrRemove)                                                                                                               //| |  |   |
                        {                                                                                                                                   //|-|--|---\c otherwise if removing
                            characterBodies.Remove(body);                                                                                                   //|-|--|---|---- remove it from the main list
                        }                                                                                                                                   //|-|--|---\e
                    }                                                                                                                                       //| |  \e
                }                                                                                                                                           //|-\e
            }                                                                                                                                               //\e
            HandleSpecial(name);                                                                                                                            //start inner-looping
            return characterBodies;                                                                                                                         //return the resultant loop
        }

        /// <summary>
        /// Internal part of the the multi-targeting system, specifically for handling the mathmaticall operations according to the mathmaticall inciter.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="characterBodies"></param>
        /// <returns></returns>
        private List<CharacterBody> handleSpecialMath(string name, List<CharacterBody> characterBodies)
        {
            //data: "all&!'8Bit Shadow':userscolour==0, 0, 0, 255&!me"
            //calls: (start-&)->{HS}->{GPBBN-&!}->{[HSM]}->{GPBBN-&!}
            //name: "all&!'8Bit Shadow':userscolour==0, 0, 0, 255&!me" -> ["'8Bit Shadow':userscolour==0, 0, 0, 255&!me"] -> "me"
            //[] = current point of this, () = main caller, {} = inner call, GPBBN = get player body/bodies by name
            List<CharacterBody> characterBodiesTMP = new List<CharacterBody>();
            string[] split = name.Split(specialOperators, StringSplitOptions.RemoveEmptyEntries);//"'8Bit Shadow':userscolour==0, 0, 0, 255", "me"
            string[] splitMath = split[0].Split(':');//"'8Bit Shadow'", "userscolour==0, 0, 0, 255"

            string[] tmp;
            if (specialMathOperators.Any(splitMath[1].Contains))
            {//if the multi-search by statistic querery contains at least one math operator
                tmp = splitMath[1].Split(specialMathOperators, StringSplitOptions.RemoveEmptyEntries);//userscolour, ('0', ' 0', ' 0', ' 255')
            }
            else
            {//otherwise tell the user they are missing an operator or the operator is not recognized by M.U.T yet
                //add a ', ' after each item in specialMathOperators using the "separatorStepBack" method
                //https://stackoverflow.com/a/43783708
                var specialMathOperatorsString = new StringBuilder();
                foreach (var item in specialMathOperators)
                {
                    if (item != specialMathOperators[specialMathOperators.Length - 2] && item != specialMathOperators[specialMathOperators.Length - 1])
                    {//if not the second last and not the last item
                        specialMathOperatorsString.Append(item).Append(", ");
                    }
                    else if (item == specialMathOperators[specialMathOperators.Length - 2])
                    {//if the current item is the second last item
                        specialMathOperatorsString.Append(item).Append(" and ");
                    }
                }

                if (specialMathOperatorsString.Length >= 1)
                {
                    specialMathOperatorsString.Length--;
                }
                throw new ConCommandException("Invalid M.U.T search parameter! Your multi-search by statistic querey does not contain a valid operator! " +
                    "Valid operation types are currently: " + specialMathOperatorsString.ToString() + ".");
            }

            string stat = tmp[0];//userscolour
            object comparision = new object();
            object comparatorVar = new object();

            string operationType = splitMath[1].Replace(stat, "").Replace(tmp[1], "");//==
            foreach (CharacterBody body in characterBodies)
            {//for every character that has not been ruled out yet
                Type type = null;
                try
                {
                    type = Type.GetType(PlayerStats.GetVariableTypeFromString(stat, body), true);//get the type of the stat
                }
                catch (Exception e)
                {
                    if (e is ConCommandException)
                    {//if the error thrown was caused by the type not existing
                        throw new ConCommandException(e.Message.ToString());//Stop further execution to safely return the game to a normal state
                    }
                    else
                    {
                        Trace.TraceError("An unknown error occured! The Type was not understood!");
                        Trace.TraceError("Please make sure the sting type matches the actual types' name in Refr.handleSpecialMath AND CC.COGetPlayersStat" +
                            "(use a breakpoint via DNSpy on Client.GetVariableTypeFromString() of the testing type to get the actual name)" +
                            " or make sure the new variables in your IValue<#type#>.GetVariableFromString() method are also present in Client.GetVariableTypeFromString()!");
                        throw new ConCommandException("M.U.T. system failed due to an error:\n" + e.Message.ToString());//Stop further execution to safely return the game to a normal state
                    }
                }

                if (type is null)
                {
                    throw new ConCommandException("An unknown error occured; type was somehow null.");
                }
                //Types:
                //float = Single
                //int = Int32
                //uint = UInt32
                //bool = Boolean
                //string = System.String
                //Vector3 = UnityEngine.Vector3
                //Color32 = UnityEngine.Color32
                //Byte = Byte

                try
                {//try to parse the comparision data
                    if (type.Name != "Vector3" && type.Name != "Color32")
                    {
                        comparision = TypeDescriptor.GetConverter(type).ConvertFromString(tmp[1]);//essentially: type.TryParse(tmp[1], out comparision)
                        //this must be done like this as 'comparision' is an object, not an actual type,
                        // creating a missmatch because of the "out #type#" and "out object" being recognised as different (where #type# is the Type)
                    }
                    else
                    {
                        if (type.Name == "Vector3")
                        {
                            string[] numbers = tmp[1].Replace(" ", "").Split(',');//0, 0, 0
                            comparision = new Vector3(float.Parse(numbers[0]), float.Parse(numbers[1]), float.Parse(numbers[2]));
                        }
                        else if (type.Name == "Color32")
                        {
                            string[] numbers = tmp[1].Replace(" ", "").Split(',');//0, 0, 0, 255
                            int[] numbersParsed = { int.Parse(numbers[0]), int.Parse(numbers[1]), int.Parse(numbers[2]), int.Parse(numbers[3]) };
                            if (numbersParsed[0] > 255 || numbersParsed[0] < 0
                             || numbersParsed[1] > 255 || numbersParsed[1] < 0
                             || numbersParsed[2] > 255 || numbersParsed[2] < 0
                             || numbersParsed[3] > 255 || numbersParsed[3] < 0)
                            {//if any numbers are more than 255 or less than 0
                                throw new ConCommandException();//go to catch
                            }

                            comparision = new Color32(Byte.Parse(numbers[0]), Byte.Parse(numbers[1]), Byte.Parse(numbers[2]), Byte.Parse(numbers[3]));
                        }

                    }

                }
                catch (Exception)
                {//if failed to parse the comparision data, get the type and tell the user the limitation of the type.
                    if (type.Name == "Single")
                    {
                        throw new ConCommandException("Please make sure the number is non-scientific, uses a '.' for the decimal and does not exceed either "
                            + float.MinValue + " or " + float.MaxValue + ", for the multi-search by statistic querey comparitor \"" + stat + "\".");
                    }
                    else if (type.Name == "Int32")
                    {
                        throw new ConCommandException("Please make sure the number is non-scientific, does not have a decimal and does not exceed either "
                            + Int32.MinValue + " or " + Int32.MaxValue + ", for the multi-search by statistic querey comparitor \"" + stat + "\".");
                    }
                    else if (type.Name == "UInt32")
                    {
                        throw new ConCommandException("Please make sure the number is non-scientific, does not have a decimal and does not exceed 0 or "
                            + UInt32.MaxValue + ", for the multi-search by statistic querey comparitor \"" + stat + "\".");
                    }
                    else if (type.Name == "Byte")
                    {
                        throw new ConCommandException("Please make sure the number is non-scientific, does not have a decimal and does not exceed either "
                            + byte.MinValue + " or " + byte.MaxValue + ", for the multi-search by statistic querey comparitor \"" + stat + "\".");
                    }
                    else if (type.Name == "Boolean")
                    {
                        throw new ConCommandException("Please make sure the right side comparision is either true or false for the multi-search by statistic querey comparitor \"" + stat + "\".");
                    }
                    else if (type.Name == "String")
                    {
                        throw new ConCommandException("for the multi-search by statistic querey comparitor \"" + stat + "\", you - somehow - managed to put in non-parsable text. Good on you.");
                    }
                    else if (type.Name == "Vector3")
                    {
                        throw new ConCommandException("Please make sure the right side follows this format: \"#,#,#\" where each '#' is a number and each number does not exceed either "
                            + Int32.MinValue + " or " + Int32.MaxValue + ", for the multi-search by statistic querey comparitor \"" + stat + "\".");
                    }
                    else if (type.Name == "Color32")
                    {

                        throw new ConCommandException("Please make sure the right side follows this format: \"#,#,#,#\" where each '#' is a number and each number does not exceed either" +
                            " 0 or 255, for the multi-search by statistic querey comparitor \"" + stat + "\".");
                    }

                    Trace.TraceError("The comparision was not understood, please make sure you add a handler for the new type added to T.M.I in refr.handleSpecialMath()!");
                    throw new ConCommandException("The comparision was not understood for the new type.");
                }

                try
                {
                    comparatorVar = PlayerStats.GetVariableObjectFromString(stat, body, type);//get the comparators' variable based on the type of the stat to get
                }
                catch (Exception e)
                {
                    if (e is ConCommandException)
                    {
                        throw new ConCommandException(e.Message.ToString());
                    }
                    else
                    {
                        Trace.TraceError("An error occured! The Type was not understood!");
                        Trace.TraceError("Please make sure the sting type matches the actual types' name in Refr.handleSpecialMath AND CC.COGetPlayersStat" +
                            "(use a breakpoint via DNSpy on Client.GetVariableTypeFromString() of the testing type to get the actual name)" +
                            " or make sure the new variables in your IValue<#type#>.GetVariableFromString() method are also present in Client.GetVariableTypeFromString()!");
                        throw new ConCommandException("The M.U.T. system cannot get the variables' data at the moment due to an error: " + e.Message.ToString());//Stop further execution to safely return the game to a normal state
                    }
                }

                if (operationType == "=" || operationType == "==")
                {
                    if (comparatorVar.Equals(comparision))
                    {//if it's a float, int, Uint or byte
                        characterBodiesTMP.Add(body);
                    }
                    else if (type.Name == "Vector3")
                    {//otherwise if it's a Vector (3)
                        if (decimal.Round((decimal)((Vector3)comparatorVar).x, 2) == decimal.Round((decimal)((Vector3)comparision).x, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).y, 2) == decimal.Round((decimal)((Vector3)comparision).y, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).z, 2) == decimal.Round((decimal)((Vector3)comparision).z, 2))
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }
                    else if (type.Name == "Color32")
                    {//otherwise if it's a colour (32)
                        if (((Color32)comparatorVar).r == ((Color32)comparision).r
                         && ((Color32)comparatorVar).g == ((Color32)comparision).g
                         && ((Color32)comparatorVar).b == ((Color32)comparision).b
                         && ((Color32)comparatorVar).a == ((Color32)comparision).a)
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }

                }
                else if (operationType == "<")
                {

                    if (type.Name == "Single" || type.Name == "Int32" || type.Name == "UInt32" || type.Name == "Byte")
                    {//if it's a float, int, Uint or byte
                        if ((Single)comparatorVar < (Single)comparision)
                        {
                            characterBodiesTMP.Add(body);
                        }
                    }
                    else if (type.Name == "Vector3")
                    {//otherwise if it's a Vector (3)
                        if (decimal.Round((decimal)((Vector3)comparatorVar).x, 2) < decimal.Round((decimal)((Vector3)comparision).x, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).y, 2) < decimal.Round((decimal)((Vector3)comparision).y, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).z, 2) < decimal.Round((decimal)((Vector3)comparision).z, 2))
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }
                    else if (type.Name == "Color32")
                    {//otherwise if it's a colour (32)
                        if (((Color32)comparatorVar).r < ((Color32)comparision).r
                         && ((Color32)comparatorVar).g < ((Color32)comparision).g
                         && ((Color32)comparatorVar).b < ((Color32)comparision).b
                         && ((Color32)comparatorVar).a < ((Color32)comparision).a)
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }

                }
                else if (operationType == ">")
                {
                    if (type.Name == "Single" || type.Name == "Int32" || type.Name == "UInt32" || type.Name == "Byte")
                    {//if it's a float, int, Uint or byte
                        if ((Single)comparatorVar > (Single)comparision)
                        {
                            characterBodiesTMP.Add(body);
                        }
                    }
                    else if (type.Name == "Vector3")
                    {//otherwise if it's a Vector (3)
                        if (decimal.Round((decimal)((Vector3)comparatorVar).x, 2) > decimal.Round((decimal)((Vector3)comparision).x, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).y, 2) > decimal.Round((decimal)((Vector3)comparision).y, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).z, 2) > decimal.Round((decimal)((Vector3)comparision).z, 2))
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }
                    else if (type.Name == "Color32")
                    {//otherwise if it's a colour (32)
                        if (((Color32)comparatorVar).r > ((Color32)comparision).r
                         && ((Color32)comparatorVar).g > ((Color32)comparision).g
                         && ((Color32)comparatorVar).b > ((Color32)comparision).b
                         && ((Color32)comparatorVar).a > ((Color32)comparision).a)
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }
                }
                else if (operationType == "<=" || operationType == "=<")
                {
                    if (type.Name == "Single" || type.Name == "Int32" || type.Name == "UInt32" || type.Name == "Byte")
                    {//if it's a float, int, Uint or byte
                        if ((Single)comparatorVar <= (Single)comparision)
                        {
                            characterBodiesTMP.Add(body);
                        }
                    }
                    else if (type.Name == "Vector3")
                    {//otherwise if it's a Vector (3)
                        if (decimal.Round((decimal)((Vector3)comparatorVar).x, 2) <= decimal.Round((decimal)((Vector3)comparision).x, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).y, 2) <= decimal.Round((decimal)((Vector3)comparision).y, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).z, 2) <= decimal.Round((decimal)((Vector3)comparision).z, 2))
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }
                    else if (type.Name == "Color32")
                    {//otherwise if it's a colour (32)
                        if (((Color32)comparatorVar).r <= ((Color32)comparision).r
                         && ((Color32)comparatorVar).g <= ((Color32)comparision).g
                         && ((Color32)comparatorVar).b <= ((Color32)comparision).b
                         && ((Color32)comparatorVar).a <= ((Color32)comparision).a)
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }
                }
                else if (operationType == ">=" || operationType == "=>")
                {
                    if (type.Name == "Single" || type.Name == "Int32" || type.Name == "UInt32" || type.Name == "Byte")
                    {//if it's a float, int, Uint or byte
                        if ((Single)comparatorVar >= (Single)comparision)
                        {
                            characterBodiesTMP.Add(body);
                        }
                    }
                    else if (type.Name == "Vector3")
                    {//otherwise if it's a Vector (3)
                        if (decimal.Round((decimal)((Vector3)comparatorVar).x, 2) >= decimal.Round((decimal)((Vector3)comparision).x, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).y, 2) >= decimal.Round((decimal)((Vector3)comparision).y, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).z, 2) >= decimal.Round((decimal)((Vector3)comparision).z, 2))
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }
                    else if (type.Name == "Color32")
                    {//otherwise if it's a colour (32)
                        if (((Color32)comparatorVar).r >= ((Color32)comparision).r
                         && ((Color32)comparatorVar).g >= ((Color32)comparision).g
                         && ((Color32)comparatorVar).b >= ((Color32)comparision).b
                         && ((Color32)comparatorVar).a >= ((Color32)comparision).a)
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }
                }
                else if (operationType == "!=")
                {
                    if (!comparatorVar.Equals(comparision))
                    {//if it's a float, int, Uint or byte
                        characterBodiesTMP.Add(body);
                    }
                    else if (type.Name == "Vector3")
                    {//otherwise if it's a Vector (3)
                        if (decimal.Round((decimal)((Vector3)comparatorVar).x, 2) == decimal.Round((decimal)((Vector3)comparision).x, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).y, 2) == decimal.Round((decimal)((Vector3)comparision).y, 2)
                         && decimal.Round((decimal)((Vector3)comparatorVar).z, 2) == decimal.Round((decimal)((Vector3)comparision).z, 2))
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }
                    else if (type.Name == "Color32")
                    {//otherwise if it's a colour (32)
                        if (((Color32)comparatorVar).r == ((Color32)comparision).r
                         && ((Color32)comparatorVar).g == ((Color32)comparision).g
                         && ((Color32)comparatorVar).b == ((Color32)comparision).b
                         && ((Color32)comparatorVar).a == ((Color32)comparision).a)
                        {//get the bodies variable and check if it's the same as the comparision
                            characterBodiesTMP.Add(body);
                        }
                    }
                }
            }

            return characterBodiesTMP;
        }

        /// <summary>
        /// Internal method looper of the multi-targeting system.
        /// </summary>
        /// <param name="name"></param>
        private void HandleSpecial(string name)
        {
            List<CharacterBody> characterBodies = new List<CharacterBody>();
            //me&!you -> me you

            //all&!me&!you -> all me you
            string[] split = name.Split(specialOperators, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 1)
            {//if there is at least one character after specialOperator
                if (name.Substring(split[0].Length, 1) == specialOperators[1])
                {//is it &
                    if (name.Substring(split[0].Length, 2) == specialOperators[0])
                    {//is it &!
                        GetPlayerBodiesByName(name.Substring(split[0].Length + 2), true);
                    }
                    else
                    {
                        GetPlayerBodiesByName(name.Substring(split[0].Length + 1));
                    }
                }
                else
                {
                    MonoBehaviour.print("An error occured while trying to handle the special operators.");
                }
            }
        }

        /// <summary>
        /// Checks if the search pattern is targeting a single player (i.e. the string does not contain any multi-search operators).
        /// </summary>
        /// <param name="name"></param>
        public void CheckIfSingleTarget(string name)
        {
            foreach (string SO in entryPoint.sanitisationSymbols)
            {
                //if (SO != "all" && SO != "me" && SO != "alive" && SO != "spectator" && SO != "local")
                if (SO != "me")
                {//dont throw when the user is using an abreiviator
                    if (name.Contains(SO))
                    {
                        throw new ConCommandException("Command must target a single player!");
                        //MonoBehaviour.print("Command must target a single player!");
                        //return;
                    }
                }
            }

        }

        /// <summary>
        /// Simply provides some real-time debugging message management through the toggleDebugging and Verbos command. Outputs to both bepinex's and the in game console.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="verbosReq"></param>
        /// <param name="callerName"></param>
        public static void print(string message, ushort verbosReq, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
        {
            if (debug && verbos >= (uint)verbosReq)
            {
                MonoBehaviour.print(callerName + ": " + message);
            }
        }

        public static bool debug = false;
        public static uint verbos = 1u;

    }
}
