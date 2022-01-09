![console overhaul](https://github.com/8BitShadow/media-resources/blob/main/console%20overhaul.png?raw=true)
# Multi-User Targeting, a "query based" user targetting tool.
## About
"Multi-User Targeting" is an expansion mod for T.M.I. which provides modders methods to fetch multiple users' `characterBodies` through queries, allowing the bodies to be fetched while in-game - further broadening the capabilities of the console command.
<br><br>

M.U.T. achieves this through multiple method loops, cutting the data into segments and adding or removing form a list of `characterBodies` according to the amount of 'additional queries' (&s) in the search and returning that list at the end of the loop. The system has multiple identifiers and 'shorthands', which both nets its complexity and flexibility, working with T.M.I. bringing it to great levels of potential use, with no (soft) limit to the amount of 'additional queries'.
<br><br>
While the mod was both specifically created so that it is possible to *fetch any characters' `characterBody`* by any matching statistic--broadening the capabilities of the console command and easing CC creation--and also made with using it outside of CC creation in mind, it is recommemened to ensure the mods' methods are called as infrequently as possible.

## Usage
The method provided will allow you, the modder, easy access to all this data, there is no additional requirements to adding the mod to your own - all you need is for the user to have T.M.I. installed and for you to use `List<CharacterBody> bodies = GetPlayerBodiesByName("query here")` somewhere. You can read how to use the query system [here (todo)]().

## due to an issue with uploading files directly into the repo via the github website, the files have been temporarily placed into a .zip file.

## development
### How can I develop for this project?
After cloning the repository and ensuring you have any version of [VS 2017/2019](https://visualstudio.microsoft.com/) installed, you should be able to simply open the `.snl` file to open the project in VS.
<br><br>
Before posting a merge request, please ensure you've:
- Adequately checked for 'top level' bugs
- Provided enough commenting/sudo-code for other contributers to quickly understand the process (if necessary)

For the sake of documenting bugfixes, when posting a merge request, please ensure you detail any changes by:
- Describing what was changed (in the head)
- How the changes where made (in the 'extended description')
  - If the merge request only adds new code and does not edit any pre-existing code, feel free to only fill the head.

### How do I compile and run this?
There are no special steps to building and compiling the code, simply press 'run' in <abbr title="Visual Studio">VS</abbr>.<br>
If you do not have the [export helper (todo)]() installed; simply press 'Ok' if an error appears saying "A project with an Output Type of Class Library cannot be started directly". Visual Studio will have the `.dll` file you need generated in `bin>Debug` for VS 2017 or `bin>Debug>netstandard2.0` for VS 2019, simply copy the `.dll` file into the BepInEx `plugins` folder and start RoR2.<br>
If you have the exporter helper tool setup correctly; after pressing 'run' in VS, simply start <abbr title="Risk of Rain 2">RoR2</abbr>.

### How can I help without any programming 'know-how'?
Simply install the mod/modpack from [the modpacks main page](https://github.com/8BtS-A-to-IA/Console-Overhaul) and play. If you encounter any issues make sure to log it and provide as much relevant detail as possible in the relevant mods' `issue` page--or the main page if you don't know which mod is the problem--after checking if the same issue has not already been encountered, you can use the [formatting guide (todo)]() to help with this.<br>
Don't worry about if you predict the wrong mod as the cause, it's more important to just have the report out there.

## Changelog:
<details>
    <summary>V1.0.0 (unreleased):</summary>
  
  - none yet!
</details>
