﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace save_switcher
{

    public class User
    {
        public User(int userID, string username)
        {
            ID = userID;
            Username = username;
        }

        public int ID { get; }
        public string Username { get; }
    }

    public class Game
    {
        public Game(int gameID, string gameName, string gameExec, string gameArgs)
        {
            ID = gameID;
            Name = gameName;
            Exec = gameExec;
            Args = gameArgs;
        }

        public int ID { get; }
        public string Name { get; }
        public string Exec { get; }
        public string Args { get; }
    }

    public class SyncEntry
    {
        public SyncEntry(int syncDefId, int userID, DateTime lastPlayed)
        {
            SyncDefinitionId = syncDefId;
            UserID = userID;
            LastPlayed = lastPlayed;
        }

        public int SyncDefinitionId { get; }
        public int UserID { get; }
        public DateTime LastPlayed { get; }

    }

    public class SyncDefinition
    {
        public SyncDefinition(int syncDefId, int gameId, string syncSource, SyncType type, string description)
        {
            SyncDefinitionId = syncDefId;
            GameID = gameId;
            SyncSource = syncSource;
            Type = type;
            Description = description;
        }

        public int SyncDefinitionId { get; }
        public int GameID { get; }
        public string SyncSource { get; }
        public SyncType Type { get; }
        public string Description { get; }
    }

    public class Sync
    {
        public Sync(string gameLocation, string applicationLocation, SyncType type)
        {
            GameLocation = gameLocation;
            ApplicationLocation = applicationLocation;
            Type = type;
        }

        public string GameLocation { get; }
        public string ApplicationLocation { get; }
        public SyncType Type { get; }
    }

    public enum SyncType
    {
        Directory = 1,
        File = 2,
        RegistryKey = 3,
    }

    public class DatabaseManager
    {

        static readonly string saveFolderLocation = Path.GetFullPath(Directory.GetCurrentDirectory() + @"\syncs");
        static readonly string dbLocation = Path.GetFullPath(saveFolderLocation + @"\save_switcher_data.db");

        readonly SQLiteConnection connection;

        public DatabaseManager()
        {
            if (!Directory.Exists(saveFolderLocation))
                Directory.CreateDirectory(saveFolderLocation);

            if (!File.Exists(dbLocation))
                InitializeDB();

            SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder();
            builder.DataSource = dbLocation;
            builder.ForeignKeys = true;

            connection = new SQLiteConnection(builder.ToString());
            connection.Open();
        }

        static void InitializeDB()
        {
             string newDatabaseString =
                @"  BEGIN TRANSACTION;

                CREATE TABLE Users ( userid INTEGER PRIMARY KEY AUTOINCREMENT, username TEXT NOT NULL UNIQUE ) STRICT;

                CREATE TABLE Games ( gameid INTEGER PRIMARY KEY AUTOINCREMENT, gamename TEXT NOT NULL, gamecmd TEXT NOT NULL, gameargs TEXT, 
                    UNIQUE (gamecmd, gameargs) ),  STRICT;

                CREATE TABLE SyncDefinitions ( syncdefid INTEGER PRIMARY KEY AUTOINCREMENT, gameid INTEGER NOT NULL REFERENCES Games (gameid) 
                    ON DELETE CASCADE ON UPDATE RESTRICT, syncsource TEXT NOT NULL, type INTEGER NOT NULL, description TEXT,
                    UNIQUE (gameid, syncsource) ) STRICT;

                CREATE TABLE SyncEntry ( syncdefid INTEGER REFERENCES SyncDefinitions (syncdefid) ON DELETE CASCADE ON UPDATE RESTRICT NOT NULL, 
                    userid INTEGER NOT NULL REFERENCES Users (userid) ON DELETE CASCADE ON UPDATE RESTRICT, 
                    lastplayed TEXT NOT NULL, UNIQUE (syncdefid, userid) ) STRICT;

                COMMIT;";

            Console.WriteLine("Creating tables...");
            SQLiteConnection conn = new SQLiteConnection("Data Source=" + dbLocation);
            conn.Open();

            SQLiteCommand cmd = new SQLiteCommand(newDatabaseString, conn);

            cmd.ExecuteNonQuery();

            conn.Close();
        }

        public bool AddUser(string username)
        {
            if (string.IsNullOrEmpty(username))
                return false;

            SQLiteCommand cmd = new SQLiteCommand(string.Format($@"INSERT INTO Users(username) VALUES (@username)"), connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@username", username));

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SQLiteException e)
            {
                if (e.ErrorCode == 19)
                    Console.WriteLine("Name is not unique!");

                return false;

            }
            return true;
        }

        public bool AddGame(string gameName, string gameExec, string gameArgs)
        {
            if (string.IsNullOrEmpty(gameName) || string.IsNullOrEmpty(gameExec))
                return false;

            string argsOrNull = gameArgs ?? null;
            SQLiteCommand cmd = new SQLiteCommand($@"INSERT INTO Games (gamename, gamecmd, gameargs) VALUES (@gameName, @gameExec, @args)", connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@gameName", gameName));
            cmd.Parameters.Add(new SQLiteParameter("@gameExec", gameExec));
            cmd.Parameters.Add(new SQLiteParameter("@args", argsOrNull));

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                throw (e);
            }


            return true;
        }

        public bool AddGame(string gameName, string gameExec)
        {
            return AddGame(gameName, gameExec, null);
        }

        public bool AddSyncDefinition(int gameID, string syncSource, SyncType type, string description)
        {
            if (syncSource == null)
                return false;

            SQLiteCommand cmd = new SQLiteCommand(string.Format($@"INSERT INTO SyncDefinitions (gameid, syncsource, type, description) VALUES (@gameID, @syncSource, @type, @description)"), connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@gameID", gameID));
            cmd.Parameters.Add(new SQLiteParameter("@syncSource", syncSource));
            cmd.Parameters.Add(new SQLiteParameter("@type", (int)type));
            cmd.Parameters.Add(new SQLiteParameter("@description", description));

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message + $"\n Arguments: \ngameID: {gameID}, syncSource: {syncSource}, type: {type}");
                throw (e);
            }

            return true;
        }

        public bool DeleteGame(int gameID)
        {
            SQLiteCommand cmd = new SQLiteCommand(string.Format($@"DELETE FROM Games WHERE gameid = @gameID"), connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@gameID", gameID));

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SQLiteException e)
            {

                return false;

            }
            return true;
        }

        public bool DeleteSyncDef(int syncDefID)
        {
            SQLiteCommand cmd = new SQLiteCommand(string.Format($@"DELETE FROM SyncDefinitions WHERE syncDefID = @syncDefID"), connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@syncDefID", syncDefID));

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SQLiteException e)
            {

                return false;

            }
            return true;
        }

        public bool DeleteUser(int userID)
        {
            SQLiteCommand cmd = new SQLiteCommand(string.Format($@"DELETE FROM Users WHERE userid = @userID"), connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@userID", userID));

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SQLiteException e)
            {

                return false;

            }
            return true;
        }

        public User GetUser(int userID)
        {

            SQLiteCommand cmd = new SQLiteCommand($@"SELECT userid, username FROM Users WHERE userid = @userID LIMIT 1", connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@userID", userID));

            SQLiteDataReader reader = cmd.ExecuteReader();
            if (reader.HasRows)
            {
                reader.Read();
                int readID = reader.GetInt32(0);
                string readName = reader.GetString(1);

                return new User(readID, readName);
            }
            else
            {
                return null;
            }
        }

        public User GetUser(string username)
        {
            SQLiteCommand cmd = new SQLiteCommand($@"SELECT userid, username FROM Users WHERE username = @username LIMIT 1", connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@username", username));


            SQLiteDataReader reader = cmd.ExecuteReader();

            if (reader.HasRows)
            {
                reader.Read();
                int readID = reader.GetInt32(0);
                string readName = reader.GetString(1);

                return new User(readID, readName);
            }
            else
            {
                return null;
            }
        }

        public User[] GetAllUsers()
        {
            SQLiteCommand cmd = new SQLiteCommand($@"SELECT userid, username FROM Users", connection);
            cmd.CommandType = System.Data.CommandType.Text;

            SQLiteDataReader reader = cmd.ExecuteReader();

            User[] users = new User[0];
            if (reader.HasRows)
            {
                bool nextRow = reader.Read();

                while (nextRow)
                {
                    int readID = reader.GetInt32(0);
                    string readName = reader.GetString(1);

                    if (users.Length > 0)
                    {
                        User[] tempUsers = new User[users.Length + 1];
                        users.CopyTo(tempUsers, 0);

                        tempUsers[tempUsers.Length - 1] = new User(readID, readName);
                        users = tempUsers;
                    }
                    else
                    {
                        users = new User[1] { new User(readID, readName) };
                    }

                    nextRow = reader.Read();
                }
                
            }
            else
            {
                return null;
            }

            return users;
        }

        public User[] GetAllUsers(int gameID)
        {
            SQLiteCommand cmd = new SQLiteCommand($@"SELECT Users.userid, Users.username FROM Users LEFT JOIN SyncEntry 
                                                   ON Users.userid = SyncEntry.userid AND SyncEntry.syncdefid = 
                                                   (SELECT SyncDefinitions.syncdefid FROM SyncDefinitions WHERE SyncDefinitions.gameid = @gameID LIMIT 1) 
                                                   ORDER BY SyncEntry.lastplayed DESC", connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@gameID", gameID));

            SQLiteDataReader reader = cmd.ExecuteReader();

            LinkedList<User> users = new LinkedList<User>();
            if (reader.HasRows)
            {
                bool nextRow = reader.Read();

                while (nextRow)
                {
                    int readID = reader.GetInt32(0);
                    string readName = reader.GetString(1);

                    users.AddLast(new User(readID, readName));

                    nextRow = reader.Read();
                }

            }
            else
            {
                return null;
            }

            return users.ToArray();
        }

        public Game GetGame(int gameid)
        {
            SQLiteCommand cmd = new SQLiteCommand($@"SELECT gameid, gamename, gamecmd, gameargs FROM Games WHERE gameid = @gameID LIMIT 1;", connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@gameID", gameid));

            SQLiteDataReader reader = cmd.ExecuteReader();

            if (reader.HasRows)
            {
                reader.Read();
                int readID = reader.GetInt32(0);
                string readName = reader.GetString(1);
                string readExec = reader.GetString(2);
                
                string readArgs = null;
                if(!reader.IsDBNull(3))
                    readArgs = reader.GetString(3);

                return new Game(readID, readName, readExec, readArgs);
            }
            else
            {
                return null;
            }
        }

        public Game[] GetAllGames()
        {
            SQLiteCommand cmd = new SQLiteCommand($@"SELECT gameid, gamename, gamecmd, gameargs FROM Games ORDER BY gamename ASC", connection);
            cmd.CommandType = System.Data.CommandType.Text;

            SQLiteDataReader reader = cmd.ExecuteReader();

            Game[] users = new Game[0];
            if (reader.HasRows)
            {

                while (reader.Read())
                {
                    int readID = reader.GetInt32(0);
                    string readName = reader.GetString(1);
                    string readCmd = reader.GetString(2);
                    
                    string readArgs = null;
                    if (!reader.IsDBNull(3))
                        readArgs = reader.GetString(3);

                    if (users.Length > 0)
                    {
                        Game[] tempUsers = new Game[users.Length + 1];
                        users.CopyTo(tempUsers, 0);

                        tempUsers[tempUsers.Length - 1] = new Game(readID, readName, readCmd, readArgs);
                        users = tempUsers;
                    }
                    else
                    {
                        users = new Game[1] { new Game(readID, readName, readCmd, readArgs) };
                    }
                }

            }
            else
            {
                return null;
            }

            return users;
        }

        public SyncDefinition[] GetAllSyncDefinitions()
        {
            SQLiteCommand cmd = new SQLiteCommand($@"SELECT syncdefid, gameid, syncsource, type, description FROM SyncDefinitions", connection);
            cmd.CommandType = System.Data.CommandType.Text;

            SQLiteDataReader reader = cmd.ExecuteReader();

            SyncDefinition[] syncDefs = new SyncDefinition[0];
            if (reader.HasRows)
            {
                bool nextRow = reader.Read();

                while (nextRow)
                {
                    int readID = reader.GetInt32(0);
                    int readGameId = reader.GetInt32(1);
                    string readSource = reader.GetString(2);
                    SyncType readType = (SyncType)reader.GetInt32(3);
                    string readComment = reader.GetString(4);

                    if (syncDefs.Length > 0)
                    {
                        SyncDefinition[] tempUsers = new SyncDefinition[syncDefs.Length + 1];
                        syncDefs.CopyTo(tempUsers, 0);

                        tempUsers[tempUsers.Length - 1] = new SyncDefinition(readID, readGameId, readSource, readType, readComment);
                        syncDefs = tempUsers;
                    }
                    else
                    {
                        syncDefs = new SyncDefinition[1] { new SyncDefinition(readID, readGameId, readSource, readType, readComment) };
                    }

                    nextRow = reader.Read();
                }

            }
            else
            {
                return null;
            }

            return syncDefs;

        }

        public SyncDefinition[] GetSyncDefinitions(int gameId)
        {
            SQLiteCommand cmd = new SQLiteCommand($@"SELECT syncdefid, gameid, syncsource, type, description FROM SyncDefinitions WHERE gameid = @gameID ORDER BY syncdefid ASC", connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@gameID", gameId));

            SQLiteDataReader reader = cmd.ExecuteReader();

            SyncDefinition[] syncDefs = new SyncDefinition[0];

            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    int readDefId = reader.GetInt32(0);
                    int readGameId = reader.GetInt32(1);
                    string readSource = reader.GetString(2);
                    SyncType readType = (SyncType)reader.GetInt32(3);
                    string readComment = reader.GetString(4);

                    SyncDefinition newEntry = new SyncDefinition(readDefId, readGameId, readSource, readType, readComment);

                    Array.Resize(ref syncDefs, syncDefs.Length + 1);

                    syncDefs[syncDefs.Length - 1] = newEntry;
                }

                return syncDefs;
            }
            else
                return null;
        }

        public Sync[] GetUserSyncs(int gameid, int userid)
        {
            SQLiteCommand cmd = new SQLiteCommand($@"SELECT SyncEntry.userid, SyncDefinitions.syncdefid, SyncDefinitions.gameid, SyncDefinitions.syncsource, SyncDefinitions.type FROM SyncDefinitions 
                LEFT JOIN SyncEntry ON (SyncEntry.userid = @userid AND SyncEntry.syncdefid = SyncDefinitions.syncdefid ) WHERE SyncDefinitions.gameid = @gameid;", connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@userid", userid));
            cmd.Parameters.Add(new SQLiteParameter("@gameid", gameid));

            SQLiteDataReader reader = cmd.ExecuteReader();

            List<Sync> syncs = new List<Sync>();
            while (reader.Read())
            {
                string destPath = reader.GetString(3);
                int syncDefId = reader.GetInt32(1);

                if (reader.IsDBNull(0))
                {
                    UpdateUserSync(userid, syncDefId);
                }

                SyncType type = (SyncType)reader.GetInt32(4);

                string sourcePath = "";
                if (type == SyncType.File || type == SyncType.Directory)
                {
                    sourcePath = $@"{saveFolderLocation.TrimEnd('\\')}\{userid}\{gameid}\{Path.GetFileName(destPath)}";
                }
                else if(type == SyncType.RegistryKey)
                {
                    sourcePath = $@"{saveFolderLocation.TrimEnd('\\')}\{userid}\{gameid}\registry_{destPath.Split('\\').Last()}";
                }

                Sync newEntry = new Sync(destPath, sourcePath, type);

                syncs.Add(newEntry);
            }

            return syncs.ToArray();
        }

        public int GetSyncDefID(int gameId, string sourcePath, SyncType type)
        {
            SQLiteCommand cmd = new SQLiteCommand($@"SELECT syncdefid FROM SyncDefinitions WHERE gameid = @gameId AND syncsource = @syncSource AND type = @type LIMIT 1", connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@gameId", gameId));
            cmd.Parameters.Add(new SQLiteParameter("@syncSource", sourcePath.Trim('\"')));
            cmd.Parameters.Add(new SQLiteParameter("@type", (int)type));

            SQLiteDataReader reader = cmd.ExecuteReader();

            if (reader.HasRows && reader.Read())
            {
                return reader.GetInt32(0);
            }
            else
                return -1;
        }

        public bool UpdateUserSync(int userid, int syncDefinitionID)
        {

            SQLiteCommand cmd = new SQLiteCommand($@"INSERT OR REPLACE INTO SyncEntry (syncdefid, userid, lastplayed) VALUES (@syncDefID, @userID, @date);", connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@syncDefID", syncDefinitionID));
            cmd.Parameters.Add(new SQLiteParameter("@userID", userid));
            cmd.Parameters.Add(new SQLiteParameter("@date", DateTime.UtcNow.Ticks));

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SQLiteException e)
            {
                DialogResult result = MessageBox.Show(e.Message + "\n" + e.ErrorCode, "Error in method UpdateSync", MessageBoxButtons.RetryCancel);
                if (result == DialogResult.Retry)
                {
                    UpdateUserSync(userid, syncDefinitionID);
                }
                else
                    throw e;
            }

            return true;
        }

        public bool UpdateUser(int userID, string newUsername)
        {
            if (string.IsNullOrEmpty(newUsername))
                return false;

            SQLiteCommand cmd = new SQLiteCommand(string.Format($@"UPDATE Users SET username = @newUsername WHERE userid = @userID"), connection);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@newUsername", newUsername));
            cmd.Parameters.Add(new SQLiteParameter("@userID", userID));

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SQLiteException e)
            {

                return false;

            }
            return true;
        }

    }
}
