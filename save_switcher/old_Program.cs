using System;
using save_switcher;
using save_switcher.Imported;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Threading;
using System.IO.IsolatedStorage;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Win32;
using System.Linq;

class old_Program
{
    public static void theMain()
    {

        Console.WriteLine("started...");
        
        DatabaseManager databaseManager = new DatabaseManager();


        while (true)
        {

            string input = Console.ReadLine().ToLower();

            switch (input)
            {
                case "add user":
                    break; //this is legacy
                    Console.WriteLine("Enter the name of the user to add:");
                    string username = Console.ReadLine();
                    if (databaseManager.AddUser(username))
                        Console.WriteLine($"User {username} succesfully added");
                    else
                        Console.WriteLine("An error occured");

                    break;

                case "add game":
                    Console.WriteLine("Enter the name of the game you want to add:");
                    string gameName = Console.ReadLine();

                    Console.WriteLine("Enter the path of the executable for this game:");
                    string gameExec = Console.ReadLine();

                    Console.WriteLine("Enter the arguments to use for this game (leave blank for nothing):");
                    string gameArgs = Console.ReadLine();

                    if (gameArgs == "")
                        gameArgs = null;

                    if (databaseManager.AddGame(gameName.Trim('"'), gameExec.Trim('"'), gameArgs))
                        Console.WriteLine("Game succesfully added");
                    else
                        Console.WriteLine("An error occured");
                    
                    break;

                case "add syncdef":
                    Console.WriteLine("Enter the game ID for this definition:");
                    string gameid = Console.ReadLine();

                    Console.WriteLine("Enter the file location for this resource:");
                    string source = Console.ReadLine().Trim(new char[] { '\"' , '\\'});

                    Console.WriteLine("Enter the type of file this is (folder, file, registrykey, 1, 2, 3):");
                    string type = Console.ReadLine();

                    Console.WriteLine("Would you like to add a comment for this definition?");
                    string comment = Console.ReadLine();

                    int parseGameId;
                    SyncType syncType;

                    try
                    {
                        parseGameId = int.Parse(gameid);
                    } catch (Exception e) {
                        Console.WriteLine("Could not parse game id!");
                        continue;
                    }


                    if (type == "1" || type == "folder")
                        syncType = SyncType.Directory;
                    else if (type == "2" || type == "file")
                        syncType = SyncType.File;
                    else if (type == "3" || type == "registrykey")
                        syncType = SyncType.RegistryKey;
                    else
                    {
                        Console.WriteLine("Type is incorrect!");
                        continue;
                    }

                    if (syncType == SyncType.Directory)
                    {
                        if (!Directory.Exists(source))
                        {
                            Console.WriteLine("Source does not exist!");
                            continue;
                        }
                    }
                    else if (syncType == SyncType.File)
                    {
                        if (!File.Exists(source))
                        {
                            Console.WriteLine("Source does not exist!");
                            continue;
                        }
                    }
                    else if (syncType == SyncType.RegistryKey)
                    {
                        RegistryKey sourceKey = RegistryHelper.GetKey(source);
                        if (sourceKey == null)
                        {
                            Console.WriteLine("Source does not exist!");
                            continue;
                        }
                    }

                    if (databaseManager.AddSyncDefinition(parseGameId, source, syncType, comment))
                        Console.WriteLine("Sync definition sucessfully added.");
                    else
                        Console.WriteLine("There was an error adding the sync definition.");

                    break;

                case "list games":
                    Game[] games = databaseManager.GetAllGames();

                    if(games != null)
                    {
                        foreach(Game game in games)
                        {
                            Console.WriteLine($"\n{game.Name}");
                            Console.WriteLine($"\tID: {game.ID}");
                            Console.WriteLine($"\tCommand: {game.Exec}");
                            Console.WriteLine($"\tArgs: {game.Args}\n");
                        }
                    }
                    break;

                case "list syncdefs":
                    SyncDefinition[] syncDefs = databaseManager.GetAllSyncDefinitions();

                    if(syncDefs != null)
                    {
                        foreach(SyncDefinition syncDef in syncDefs)
                        {
                            Console.WriteLine($"\n{syncDef.SyncDefinitionId}:");
                            Console.WriteLine($"\tGameID: {syncDef.GameID}");
                            Console.WriteLine($"\tLocation: {syncDef.SyncSource}");
                            Console.WriteLine($"\tType: {syncDef.Type}");
                        }
                    }
                    break;

                case "start":
                    return; //this is now legacy
                    Console.WriteLine("Enter the user you want to start as:");
                    string name = Console.ReadLine();

                    User user = databaseManager.GetUser(name);

                    if (!Equals(user, null))
                    {
                        Console.WriteLine("Enter the game you want to run:");
                        string gameRead = Console.ReadLine();

                        Game game = databaseManager.GetGame(Int32.Parse(gameRead));

                        if (!Equals(game, null))
                        {
                            Sync[] syncs = databaseManager.GetUserSyncs(game.ID, user.ID);

                            foreach (Sync sync in syncs)
                            {
                                if (sync.Type == SyncType.Directory)
                                {
                                    //is directory
                                    //Directory.Delete(sync.Destination, true);
                                    Directory.Move(sync.GameLocation, sync.GameLocation.Trim('\\') + "_temp");
                                    Directory.CreateDirectory(sync.GameLocation);

                                    CopyDirectory(sync.ApplicationLocation, sync.GameLocation);
                                }
                                else if (sync.Type == SyncType.File)
                                {
                                    //is file
                                    File.Move(sync.GameLocation, sync.GameLocation.Trim('\\') + "_temp");

                                    if (sync.ApplicationLocation != null)
                                    {
                                        File.Copy(sync.ApplicationLocation, sync.GameLocation, true);
                                        Console.WriteLine($"Copying {sync.ApplicationLocation} --> {sync.GameLocation}");
                                    }
                                    
                                }
                            }

                            if (syncs.Length == 0)
                                Console.WriteLine("No syncs found, starting...");

                            Process proc = new Process();
                            proc.StartInfo.FileName = game.Exec; 

                            if (!Equals(game.Args, null))
                                proc.StartInfo.Arguments = game.Args;

                            proc.Start();
                            proc.WaitForExit();
                            proc.Dispose();

                            foreach (Sync sync in syncs)
                            {

                                if(sync.Type == SyncType.Directory)
                                {
                                    if(Directory.Exists(sync.ApplicationLocation))
                                        Directory.Move(sync.ApplicationLocation, $"{sync.ApplicationLocation}_{DateTime.UtcNow.Ticks}");

                                    CopyDirectory(sync.GameLocation, sync.ApplicationLocation);

                                    Directory.Delete(sync.GameLocation, true);
                                    Directory.Move(sync.GameLocation.Trim('\\') + "_temp", sync.GameLocation);
                                }
                                else if (sync.Type == SyncType.File)
                                {
                                    if(File.Exists(sync.ApplicationLocation))
                                        File.Move(sync.ApplicationLocation, $"{sync.ApplicationLocation}_{DateTime.UtcNow.Ticks}");

                                    File.Copy(sync.GameLocation, sync.ApplicationLocation, false);

                                    File.Delete(sync.GameLocation);
                                    File.Move(sync.GameLocation.Trim('\\') + "_temp", sync.GameLocation);
                                }

                                int syncDefId = databaseManager.GetSyncDefID(game.ID, sync.GameLocation, sync.Type);

                                if (syncDefId >= 0)
                                    databaseManager.UpdateUserSync(user.ID, syncDefId);
                            }

                        }
                        else
                            Console.WriteLine("Error");
                    }
                    else
                        Console.WriteLine("An Error Occured");

                    break;

                case "help":
                    Console.WriteLine("Commands:\n");
                    Console.WriteLine("\tadd game");
                    Console.WriteLine("\tadd syncdef");
                    Console.WriteLine("\tlist games");
                    Console.WriteLine("\tlist syncdefs");
                    break;
            }
            
        }
        
    }

    private static Process GetProcess(string path)
    {
        Process[] plist = Process.GetProcesses();

        for (int i = 0; i < plist.Length; i++)
        {
            StringBuilder builder = new StringBuilder(Int16.MaxValue);

            IntPtr ptr = Kernel32.OpenProcess(0x00001000, false, plist[i].Id);
            int wordSize = Int16.MaxValue;

            if (Kernel32.QueryFullProcessImageName(ptr, 0, builder, ref wordSize))
                if (builder.ToString().ToLower() == path.ToLower())
                    return plist[i];
        }

        return null;
    }

    public static void CopyDirectory(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir))
            return;

        Console.WriteLine($"Copying directory {sourceDir} --> {destDir}");

        if (destDir.Contains(sourceDir))
            return;

        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        string[] files = Directory.GetFiles(sourceDir);

        foreach (string file in files)
        {
            File.Copy(file, destDir + $@"\{Path.GetFileName(file)}", true);
        }

        string[] dirs = Directory.GetDirectories(sourceDir);

        foreach (string directory in dirs)
        {
            CopyDirectory(directory, $@"{destDir}\{Path.GetFileName(directory)}");
        }
    }
}

