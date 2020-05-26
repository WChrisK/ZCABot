using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Discord;

namespace ZCABot
{
    public partial class Bot
    {
        private const int eventLoopPulseMilliseconds = 10000;
        private const string timeoutFilePath = "timeout.txt";

        private volatile bool continueEventLoopThread;
        private readonly List<RoleTimeout> roleTimeouts = new List<RoleTimeout>();
        private Thread? eventLoopThread;

        private void StartEventLoopThread()
        {
            CreateTimeoutFileIfMissing();
            ReadTimeoutFile();

            continueEventLoopThread = true;

            eventLoopThread = new Thread(() =>
            {
                while (continueEventLoopThread)
                {
                    try
                    {
                        CheckRollTimeouts();
                    }
                    finally
                    {
                        Thread.Sleep(eventLoopPulseMilliseconds);
                    }
                }
            });

            eventLoopThread.Start();
        }

        private void ReadTimeoutFile()
        {
            roleTimeouts.Clear();

            try
            {
                string[] lines = File.ReadAllLines(timeoutFilePath);

                foreach (string line in lines)
                {
                    string[] tokens = line.Split(" ");
                    ulong.TryParse(tokens[0], out ulong userID);
                    ulong.TryParse(tokens[1], out ulong roleID);
                    long.TryParse(tokens[2], out long timeBits);

                    DateTime dateTime = DateTime.FromBinary(timeBits);
                    RoleTimeout roleTimeout = new RoleTimeout(userID, roleID, dateTime);
                    roleTimeouts.Add(roleTimeout);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"ERROR: Unexpected error when reading timeout file: {e.Message}");
                throw;
            }

            Console.WriteLine($"Read {roleTimeouts.Count} temporary roles from timeout file");
        }

        private void CreateTimeoutFileIfMissing()
        {
            try
            {
                if (!File.Exists(timeoutFilePath))
                    File.Create(timeoutFilePath).Close();
            }
            catch (Exception e)
            {
                Console.WriteLine($"ERROR: Unable to create timeout file at: {timeoutFilePath}");
                Console.WriteLine($"Reason: {e.Message}");
                throw;
            }
        }

        private void WriteRoleTimeouts()
        {
            using StreamWriter writer = File.CreateText(timeoutFilePath);

            foreach (RoleTimeout roleTimeout in roleTimeouts)
                writer.WriteLine($"{roleTimeout.UserID} {roleTimeout.RoleID} {roleTimeout.Expiration.Ticks}");

            Console.WriteLine($"Wrote {roleTimeouts.Count} role timeouts");
        }

        private void AddRoleTimeout(IGuildUser user, IRole role, DateTime removalDateTime)
        {
            RoleTimeout roleTimeout = new RoleTimeout(user.Id, role.Id, removalDateTime);
            roleTimeouts.Add(roleTimeout);

            WriteRoleTimeouts();
        }

        private void CheckRollTimeouts()
        {
            List<RoleTimeout> timeoutsToRemove = new List<RoleTimeout>();

            foreach (RoleTimeout roleTimeout in roleTimeouts.ToList())
            {
                if (roleTimeout.Expiration >= DateTime.Now)
                    continue;

                timeoutsToRemove.Add(roleTimeout);

                IGuildUser? user = Guild.GetUserAsync(roleTimeout.UserID).Result;
                if (user == null)
                {
                    Console.WriteLine($"ERROR: Cannot find user {roleTimeout.UserID}");
                    continue;
                }

                IRole? role = Guild.GetRole(roleTimeout.RoleID);
                if (role == null)
                {
                    Console.WriteLine($"ERROR: Cannot find role {roleTimeout.UserID}");
                    continue;
                }

                Log($"Removing role for user ID {user.Username}");
                user.RemoveRoleAsync(role).Wait();
            }

            if (timeoutsToRemove.Count > 0)
            {
                roleTimeouts.RemoveAll(rt => timeoutsToRemove.Contains(rt));
                WriteRoleTimeouts();
            }
        }
    }
}
