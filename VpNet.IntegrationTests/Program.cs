﻿using System;

namespace VpNet.IntegrationTests
{
    class Program
    {
        static ManagedApi.Instance instance;

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            instance = new ManagedApi.Instance();
            MainAsync(args);
            instance.OnAvatarEnter += Instance_OnAvatarEnter;
            instance.OnAvatarLeave += Instance_OnAvatarLeave;
            Console.ReadKey();

        }

        private static void Instance_OnAvatarLeave(ManagedApi.Instance sender, AvatarLeaveEventArgs args)
        {
            sender.ConsoleMessage($"{args.Avatar.Name} has left {instance.Configuration.World.Name}");
        }

        private static void Instance_OnAvatarEnter(ManagedApi.Instance sender, AvatarEnterEventArgs args)
        {
            sender.ConsoleMessage(args.Avatar, "greetings", $"Welcome to {instance.Configuration.World.Name}, {args.Avatar.Name}.");
        }

        static async void MainAsync(string[] args)
        {
            await instance.ConnectAsync();
            await instance.LoginAsync("<<your username here>>", "<<yourpassword>>", "<<yourbotname>>");
            await instance.EnterAsync("<<world here>>");
            instance.UpdateAvatar();
        }
    }
}
