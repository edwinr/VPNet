﻿#region Copyright notice
/*
____   ___.__         __               .__    __________                        .__.__
\   \ /   |__________/  |_ __ _______  |  |   \______   _____ ____________    __| _|__| ______ ____
 \   Y   /|  \_  __ \   __|  |  \__  \ |  |    |     ___\__  \\_  __ \__  \  / __ ||  |/  ____/ __ \
  \     / |  ||  | \/|  | |  |  // __ \|  |__  |    |    / __ \|  | \// __ \/ /_/ ||  |\___ \\  ___/
   \___/  |__||__|   |__| |____/(____  |____/  |____|   (____  |__|  (____  \____ ||__/____  >\___  >
                                     \/                      \/           \/     \/        \/     \/
    This file is part of VPNET Version 1.0

    Copyright (c) 2012-2016 CUBE3 (Cit:36)

    VPNET is free software: you can redistribute it and/or modify it under the terms of the
    GNU Lesser General Public License (LGPL) as published by the Free Software Foundation, either
    version 2.1 of the License, or (at your option) any later version.

    VPNET is distributed in the hope that it will be useful,but WITHOUT ANY WARRANTY; without even
    the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the LGPL License
    for more details.

    You should have received a copy of the GNU Lesser General Public License (LGPL) along with VPNET.
    If not, see <http://www.gnu.org/licenses/>.
*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VpNet.Cache;
using VpNet.Extensions;
using VpNet.Interfaces;
using VpNet.ManagedApi.System;
using VpNet.NativeApi;

namespace VpNet.Abstract
{
    /// <summary>
    /// Abtract fully teamplated instance class, providing .NET encapsulation strict templated types to the native C wrapper.
    /// </summary>
    /// <typeparam name="T">Type of the abstract implementation</typeparam>
    /// <typeparam name="TAvatar">The type of the avatar.</typeparam>
    /// <typeparam name="TFriend">The type of the friend.</typeparam>
    /// <typeparam name="void">The type of the result.</typeparam>
    /// <typeparam name="TTerrainCell">The type of the terrain cell.</typeparam>
    /// <typeparam name="TTerrainNode">The type of the terrain node.</typeparam>
    /// <typeparam name="TTerrainTile">The type of the terrain tile.</typeparam>
    /// <typeparam name="TVpObject">The type of the vp object.</typeparam>
    /// <typeparam name="TWorld">The type of the world.</typeparam>
    /// <typeparam name="TCell">The type of the cell.</typeparam>
    /// <typeparam name="TChatMessage">The type of the chat message.</typeparam>
    /// <typeparam name="TTerrain">The type of the terrain.</typeparam>
    /// <typeparam name="TUniverse">The type of the universe.</typeparam>
    /// <typeparam name="TTeleport">The type of the teleport.</typeparam>
    [Serializable]
    public abstract partial class BaseInstanceT<T,
        /* Scene Type specifications ----------------------------------------------------------------------------------------------------------------------------------------------*/
        TAvatar, TFriend, TTerrainCell, TTerrainNode,
        TTerrainTile, TVpObject, TWorld, TCell,TChatMessage,TTerrain,TUniverse, TTeleport,
        TUserAttributes
        > :
        /* Interface specifications -----------------------------------------------------------------------------------------------------------------------------------------*/
        /* Functions */
        BaseInstanceEvents<TWorld>,
        IAvatarFunctions<TAvatar>,
        IChatFunctions<TAvatar>,
        IFriendFunctions<TFriend>,
        ITeleportFunctions<TWorld, TAvatar>,
        ITerrainFunctions<TTerrainTile, TTerrainNode, TTerrainCell>,
        IVpObjectFunctions<TVpObject>,
        IWorldFunctions<TWorld>,
        IUniverseFunctions
        /* Constraints ----------------------------------------------------------------------------------------------------------------------------------------------------*/
        where TUniverse : class, IUniverse, new()
        where TTerrain : class, ITerrain, new()
        where TCell : class, ICell, new()
        where TChatMessage : class, IChatMessage, new()
        where TTerrainCell : class, ITerrainCell, new()
        where TTerrainNode : class, ITerrainNode<TTerrainTile,TTerrainNode,TTerrainCell>, new()
        where TTerrainTile : class, ITerrainTile<TTerrainTile,TTerrainNode, TTerrainCell>, new()
        where TWorld : class, IWorld, new()
        where TAvatar : class, IAvatar, new()
        where TFriend : class, IFriend, new()
        where TVpObject : class, IVpObject, new()
        where TTeleport : class, ITeleport<TWorld,TAvatar>, new()
        where TUserAttributes : class, IUserAttributes, new()
        where T : class, new()
    {
        bool _isInitialized;

        public OpCacheProvider ModelCacheProvider { get; internal set; }

        private readonly Dictionary<int, TaskCompletionSource<object>> _objectCompletionSources = new Dictionary<int, TaskCompletionSource<object>>();

        private Dictionary<int, TAvatar> _avatars;

        public T Implementor { get; set; }

        Dictionary<string, TWorld> _worlds;

        private TUniverse Universe { get; set; }
        private TWorld World { get; set; }
        private NetConfig netConfig;
        private GCHandle instanceHandle;
        private TaskCompletionSource<object> ConnectCompletionSource;
        private TaskCompletionSource<object> LoginCompletionSource;
        private TaskCompletionSource<object> EnterCompletionSource;

        internal void Init()
        {
            Universe = new TUniverse();
            World = new TWorld();
            ((IAvatarFunctions<TAvatar>) this).Avatars = new Dictionary<int, TAvatar>();
            _avatars =  ((IAvatarFunctions<TAvatar>) this).Avatars;
            _worlds = new Dictionary<string, TWorld>();
            _isInitialized = true;
        }

        internal void InitOnce()
        {
            instanceHandle = GCHandle.Alloc(this);
            netConfig.Context = GCHandle.ToIntPtr(instanceHandle);
            netConfig.Create = Connection.CreateNative;
            netConfig.Destroy = Connection.DestroyNative;
            netConfig.Connect = Connection.ConnectNative;
            netConfig.Receive = Connection.ReceiveNative;
            netConfig.Send = Connection.SendNative;
            netConfig.Timeout = Connection.TimeoutNative;

            OnChatNativeEvent += OnChatNative;
            OnAvatarAddNativeEvent += OnAvatarAddNative;
            OnAvatarChangeNativeEvent += OnAvatarChangeNative;
            OnAvatarDeleteNativeEvent += OnAvatarDeleteNative;
            OnAvatarClickNativeEvent += OnAvatarClickNative;
            OnWorldListNativeEvent += OnWorldListNative;
            OnWorldDisconnectNativeEvent += OnWorldDisconnectNative;

            OnObjectChangeNativeEvent += OnObjectChangeNative;
            OnObjectCreateNativeEvent += OnObjectCreateNative;
            OnObjectClickNativeEvent += OnObjectClickNative;
            OnObjectBumpNativeEvent += OnObjectBumpNative;
            OnObjectBumpEndNativeEvent += OnObjectBumpEndNative;
            OnObjectDeleteNativeEvent += OnObjectDeleteNative;

            OnQueryCellEndNativeEvent += OnQueryCellEndNative;
            OnUniverseDisconnectNativeEvent += OnUniverseDisconnectNative;
            OnTeleportNativeEvent += OnTeleportNative;
            OnUserAttributesNativeEvent += OnUserAttributesNative;
            OnJoinNativeEvent += OnJoinNative;

            OnObjectCreateCallbackNativeEvent += OnObjectCreateCallbackNative;
            OnObjectChangeCallbackNativeEvent += OnObjectChangeCallbackNative;
            OnObjectDeleteCallbackNativeEvent += OnObjectDeleteCallbackNative;
            OnObjectGetCallbackNativeEvent += OnObjectGetCallbackNative;
            this.OnObjectLoadCallbackNativeEvent += this.OnObjectLoadCallbackNative;

            OnFriendAddCallbackNativeEvent += OnFriendAddCallbackNative;
            OnFriendDeleteCallbackNativeEvent += OnFriendDeleteCallbackNative;
            OnGetFriendsCallbackNativeEvent += OnGetFriendsCallbackNative;
        }

        private bool HasParentInstance { get;set; }

        internal protected BaseInstanceT(BaseInstanceEvents<TWorld> parentInstance)
        {
            HasParentInstance = true;
            _instance = parentInstance._instance;
            Init();
            _avatars = ((IAvatarFunctions<TAvatar>) parentInstance).Avatars;
            Configuration = parentInstance.Configuration;
            Configuration.IsChildInstance = true;
            parentInstance.OnChatNativeEvent += OnChatNative;
            parentInstance.OnAvatarAddNativeEvent += OnAvatarAddNative;
            parentInstance.OnAvatarChangeNativeEvent += OnAvatarChangeNative;
            parentInstance.OnAvatarDeleteNativeEvent += OnAvatarDeleteNative;
            parentInstance.OnAvatarClickNativeEvent += OnAvatarClickNative;
            parentInstance.OnWorldListNativeEvent += OnWorldListNative;
            parentInstance.OnWorldDisconnectNativeEvent += OnWorldDisconnectNative;

            parentInstance.OnObjectChangeNativeEvent += OnObjectChangeNative;
            parentInstance.OnObjectCreateNativeEvent += OnObjectCreateNative;
            parentInstance.OnObjectClickNativeEvent += OnObjectClickNative;
            parentInstance.OnObjectBumpNativeEvent += OnObjectBumpNative;
            parentInstance.OnObjectBumpEndNativeEvent += OnObjectBumpEndNative;
            parentInstance.OnObjectDeleteNativeEvent += OnObjectDeleteNative;
            parentInstance.OnQueryCellEndNativeEvent += OnQueryCellEndNative;
            parentInstance.OnUniverseDisconnectNativeEvent += OnUniverseDisconnectNative;
            parentInstance.OnTeleportNativeEvent += OnTeleportNative;
            parentInstance.OnUserAttributesNativeEvent += OnUserAttributesNative;
            parentInstance.OnJoinNativeEvent += OnJoinNative;

            parentInstance.OnObjectCreateCallbackNativeEvent += OnObjectCreateCallbackNative;
            parentInstance.OnObjectChangeCallbackNativeEvent += OnObjectChangeCallbackNative;
            parentInstance.OnObjectDeleteCallbackNativeEvent += OnObjectDeleteCallbackNative;
            parentInstance.OnObjectGetCallbackNativeEvent += OnObjectGetCallbackNative;

            parentInstance.OnFriendAddCallbackNativeEvent += OnFriendAddCallbackNative;
            parentInstance.OnFriendDeleteCallbackNativeEvent += OnFriendDeleteCallbackNative;
            parentInstance.OnGetFriendsCallbackNativeEvent += OnGetFriendsCallbackNative;
        }

        protected BaseInstanceT(InstanceConfiguration<TWorld> configuration)
        {
            InitOnce();
            InitVpNative();
            // this can't be a child instance.
            configuration.IsChildInstance = false;
            Configuration = configuration;
        }

        private void InitVpNative()
        {
            if (!_isInitialized)
            {
                Init();
                Configuration = new InstanceConfiguration<TWorld>(false);
                int rc = Functions.vp_init(4);
                if (rc != 0)
                {
                    if (rc != 3)
                        throw new VpException((ReasonCode)rc);
                    //vp previously initialized. do nothing.
                }

            }
            _instance = Functions.vp_create(ref netConfig);

            SetNativeEvent(Events.Chat, OnChatNative1);
            SetNativeEvent(Events.AvatarAdd, OnAvatarAddNative1);
            SetNativeEvent(Events.AvatarChange, OnAvatarChangeNative1);
            SetNativeEvent(Events.AvatarDelete, OnAvatarDeleteNative1);
            SetNativeEvent(Events.AvatarClick, OnAvatarClickNative1);
            SetNativeEvent(Events.WorldList, OnWorldListNative1);
            SetNativeEvent(Events.WorldSetting, OnWorldSettingNative1);
            SetNativeEvent(Events.WorldSettingsChanged, OnWorldSettingsChangedNative1);
            SetNativeEvent(Events.ObjectChange, OnObjectChangeNative1);
            SetNativeEvent(Events.Object, OnObjectCreateNative1);
            SetNativeEvent(Events.ObjectClick, OnObjectClickNative1);
            SetNativeEvent(Events.ObjectBumpBegin, OnObjectBumpNative1);
            SetNativeEvent(Events.ObjectBumpEnd, OnObjectBumpEndNative1);
            SetNativeEvent(Events.ObjectDelete, OnObjectDeleteNative1);
            SetNativeEvent(Events.QueryCellEnd, OnQueryCellEndNative1);
            SetNativeEvent(Events.UniverseDisconnect, OnUniverseDisconnectNative1);
            SetNativeEvent(Events.WorldDisconnect, OnWorldDisconnectNative1);
            SetNativeEvent(Events.Teleport, OnTeleportNative1);
            SetNativeEvent(Events.UserAttributes, OnUserAttributesNative1);
            SetNativeEvent(Events.Join, OnJoinNative1);
            SetNativeCallback(Callbacks.ObjectAdd, OnObjectCreateCallbackNative1);
            SetNativeCallback(Callbacks.ObjectChange, OnObjectChangeCallbackNative1);
            SetNativeCallback(Callbacks.ObjectDelete, OnObjectDeleteCallbackNative1);
            SetNativeCallback(Callbacks.ObjectGet, OnObjectGetCallbackNative1);
            SetNativeCallback(Callbacks.ObjectLoad, this.OnObjectLoadCallbackNative1);
            SetNativeCallback(Callbacks.FriendAdd, OnFriendAddCallbackNative1);
            SetNativeCallback(Callbacks.FriendDelete, OnFriendDeleteCallbackNative1);
            SetNativeCallback(Callbacks.GetFriends, OnGetFriendsCallbackNative1);
            SetNativeCallback(Callbacks.Login, OnLoginCallbackNative1);
            SetNativeCallback(Callbacks.Enter, OnEnterCallbackNativeEvent1);
            //SetNativeCallback(Callbacks.Join, OnJoinCallbackNativeEvent1);
            SetNativeCallback(Callbacks.ConnectUniverse, OnConnectUniverseCallbackNative1);
            //SetNativeCallback(Callbacks.WorldPermissionUserSet, OnWorldPermissionUserSetCallbackNative1);
            //SetNativeCallback(Callbacks.WorldPermissionSessionSet, OnWorldPermissionSessionSetCallbackNative1);
            //SetNativeCallback(Callbacks.WorldSettingSet, OnWorldSettingsSetCallbackNative1);
        }

        protected BaseInstanceT()
        {
            InitOnce();
            InitVpNative();
        }

        internal void OnObjectCreateCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnObjectCreateCallbackNativeEvent(instance, rc, reference); } }
        internal void OnObjectChangeCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnObjectChangeCallbackNativeEvent(instance, rc, reference); } }
        internal void OnObjectDeleteCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnObjectDeleteCallbackNativeEvent(instance, rc, reference); } }
        internal void OnObjectGetCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnObjectGetCallbackNativeEvent(instance, rc, reference); } }
        internal void OnFriendAddCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnFriendAddCallbackNativeEvent(instance, rc, reference); } }
        internal void OnFriendDeleteCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnFriendDeleteCallbackNativeEvent(instance, rc, reference); } }
        internal void OnGetFriendsCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnFriendDeleteCallbackNativeEvent(instance, rc, reference); } }
        internal void OnObjectLoadCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnObjectLoadCallbackNativeEvent(instance, rc, reference); } }
        internal void OnLoginCallbackNative1(IntPtr instance, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(LoginCompletionSource, rc, null);
            }
        }
        internal void OnEnterCallbackNativeEvent1(IntPtr instance, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(EnterCompletionSource, rc, null);
            }
        }
        internal void OnJoinCallbackNativeEvent1(IntPtr instance, int rc, int reference) { lock (this) { OnJoinCallbackNativeEvent(instance, rc, reference); } }
        internal void OnConnectUniverseCallbackNative1(IntPtr instance, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(ConnectCompletionSource, rc, null);
            }
        }
        internal void OnWorldPermissionUserSetCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnWorldPermissionUserSetCallbackNativeEvent(instance, rc, reference); } }
        internal void OnWorldPermissionSessionSetCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnWorldPermissionSessionSetCallbackNativeEvent(instance, rc, reference); } }
        internal void OnWorldSettingsSetCallbackNative1(IntPtr instance, int rc, int reference) { lock (this) { OnWorldSettingsSetCallbackNativeEvent(instance, rc, reference); } }



        internal void OnChatNative1(IntPtr instance) { lock (this) { OnChatNativeEvent(instance); } }
        internal void OnAvatarAddNative1(IntPtr instance) { lock (this) { OnAvatarAddNativeEvent(instance); } }
        internal void OnAvatarChangeNative1(IntPtr instance) { lock (this) { OnAvatarChangeNativeEvent(instance); } }
        internal void OnAvatarDeleteNative1(IntPtr instance) { lock (this) { OnAvatarDeleteNativeEvent(instance); } }
        internal void OnAvatarClickNative1(IntPtr instance) { lock (this) { OnAvatarClickNativeEvent(instance); } }
        internal void OnWorldListNative1(IntPtr instance) { lock (this) { OnWorldListNativeEvent(instance); } }
        internal void OnWorldDisconnectNative1(IntPtr instance) { lock (this) { OnWorldDisconnectNativeEvent(instance); } }
        internal void OnWorldSettingsChangedNative1(IntPtr instance) { lock (this) { OnWorldSettingsChangedNativeEvent(instance); } }
        internal void OnWorldSettingNative1(IntPtr instance) { lock (this) { OnWorldSettingNativeEvent(instance); } }
        internal void OnObjectChangeNative1(IntPtr instance) { lock (this) { OnObjectChangeNativeEvent(instance); } }
        internal void OnObjectCreateNative1(IntPtr instance) { lock (this) { OnObjectCreateNativeEvent(instance); } }
        internal void OnObjectClickNative1(IntPtr instance) { lock (this) { OnObjectClickNativeEvent(instance); } }
        internal void OnObjectBumpNative1(IntPtr instance) { lock (this) { OnObjectBumpNativeEvent(instance); } }
        internal void OnObjectBumpEndNative1(IntPtr instance) { lock (this) { OnObjectBumpEndNativeEvent(instance); } }
        internal void OnObjectDeleteNative1(IntPtr instance) { lock (this) { OnObjectDeleteNativeEvent(instance); } }
        internal void OnQueryCellEndNative1(IntPtr instance) { lock (this) { OnQueryCellEndNativeEvent(instance); } }
        internal void OnUniverseDisconnectNative1(IntPtr instance) { lock (this) { OnUniverseDisconnectNativeEvent(instance); } }
        internal void OnTeleportNative1(IntPtr instance) { lock (this) { OnTeleportNativeEvent(instance); } }
        internal void OnUserAttributesNative1(IntPtr instance) { lock (this){OnUserAttributesNativeEvent(instance);} }
        internal void OnJoinNative1(IntPtr instance) { lock (this) { OnJoinNativeEvent(instance); } }

        #region Methods

        private void SetCompletionResult(int referenceNumber, int rc, object result)
        {
            var tcs = _objectCompletionSources[referenceNumber];
            SetCompletionResult(tcs, rc, result);
        }

        private static void SetCompletionResult(TaskCompletionSource<object> tcs, int rc, object result)
        {
            if (rc != 0)
            {
                tcs.SetException(new VpException((ReasonCode)rc));
            }
            else
            {
                tcs.SetResult(result);
            }
        }

        private static void CheckReasonCode(int rc)
        {
            if (rc != 0)
            {
                throw new VpException((ReasonCode)rc);
            }
        }

        #region IUniverseFunctions Implementations

        virtual public Task ConnectAsync(string host = "universe.virtualparadise.org", ushort port = 57000)
        {
            Universe.Host = host;
            Universe.Port = port;

            lock (this)
            {
                ConnectCompletionSource = new TaskCompletionSource<object>();
                var rc = Functions.vp_connect_universe(_instance, host, port);
                if (rc != 0)
                {
                    return Task.FromException(new VpException((ReasonCode)rc));
                }
                return ConnectCompletionSource.Task;
            }
        }

        virtual public async Task LoginAndEnterAsync(bool announceAvatar = true)
        {
            await ConnectAsync();
            await LoginAsync();
            if (announceAvatar)
            {
                await EnterAsync();
                UpdateAvatar();
            }
            await EnterAsync();
        }

        virtual public async Task LoginAsync()
        {
            if (Configuration == null ||
                string.IsNullOrEmpty(Configuration.BotName) ||
                string.IsNullOrEmpty(Configuration.Password) ||
                string.IsNullOrEmpty(Configuration.UserName)
                )
            {
                throw new ArgumentException("Can't login because of Incomplete login configuration.");
            }

            await LoginAsync(Configuration.UserName, Configuration.Password, Configuration.BotName);
        }

        virtual public Task LoginAsync(string username, string password, string botname)
        {
            lock (this)
            {
                Configuration.BotName = botname;
                Configuration.UserName = username;
                Configuration.Password = password;

                LoginCompletionSource = new TaskCompletionSource<object>();
                var rc = Functions.vp_login(_instance, username, password, botname);
                if (rc != 0)
                {
                    return Task.FromException(new VpException((ReasonCode)rc));
                }

                return LoginCompletionSource.Task;
            }
        }

        #endregion

        #region IWorldFunctions Implementations
        [Obsolete("No longer necessary for network IO to occur")]
        virtual public void Wait(int milliseconds = 10)
        {
            Thread.Sleep(milliseconds);
        }

        virtual public Task EnterAsync(string worldname)
        {
            return EnterAsync(new TWorld { Name = worldname });
        }

        virtual public Task EnterAsync()
        {
            if (Configuration == null || Configuration.World == null || string.IsNullOrEmpty(Configuration.World.Name))
                throw new ArgumentException("Can't login because of Incomplete instance world configuration.");
            return EnterAsync(Configuration.World);
        }

        virtual public Task EnterAsync(TWorld world)
        {
            lock (this)
            {
                Configuration.World = world;

                EnterCompletionSource = new TaskCompletionSource<object>();
                var rc = Functions.vp_enter(_instance, world.Name);
                if (rc != 0)
                {
                    return Task.FromException(new VpException((ReasonCode)rc));
                }

                return EnterCompletionSource.Task;
            }
        }

        virtual public TAvatar My()
        {
            return new TAvatar
            {
                UserId = Functions.vp_int(_instance, IntegerAttribute.MyUserId),
                Name = Configuration.BotName,
                AvatarType = Functions.vp_int(_instance, IntegerAttribute.MyType),
                Position = new Vector3
                {
                    X = Functions.vp_double(_instance, FloatAttribute.MyX),
                    Y = Functions.vp_double(_instance, FloatAttribute.MyY),
                    Z = Functions.vp_double(_instance, FloatAttribute.MyZ)
                },
                Rotation = new Vector3
                {
                    X = Functions.vp_double(_instance, FloatAttribute.MyPitch),
                    Y = Functions.vp_double(_instance, FloatAttribute.MyYaw),
                    Z = 0 /* roll currently not supported*/
                },
                LastChanged = DateTime.Now
            };
        }

        /// <summary>
        /// Leave the current world
        /// </summary>
        virtual public void Leave()
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_leave(_instance));
                if (OnWorldLeave !=null)
                {
                    OnWorldLeave(Implementor,new WorldLeaveEventArgsT<TWorld> {World = Configuration.World});
                }
            }
        }

        virtual public void Disconnect()
        {
            _avatars.Clear();
            Functions.vp_destroy(_instance);
            _isInitialized = false;
            InitVpNative();
            if (OnUniverseDisconnect != null)
                OnUniverseDisconnect(Implementor, new UniverseDisconnectEventArgsT<TUniverse> { Universe = Universe,DisconnectType = VpNet.DisconnectType.UserDisconnected  });
        }

        virtual public void ListWorlds()
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_list(_instance, 0));
            }
        }

        #endregion

        #region IQueryCellFunctions Implementation

        virtual public void QueryCell(int cellX, int cellZ)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_query_cell(_instance, cellX, cellZ));
            }
        }

        virtual public void QueryCell(int cellX, int cellZ, int revision)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_query_cell_revision(_instance, cellX, cellZ, revision));
            }
        }

        #endregion

        #region IVpObjectFunctions implementations

        public void ClickObject(TVpObject vpObject)
        {
            lock (this)
            {
                ClickObject(vpObject.Id);
            }
        }

        public void ClickObject(int objectId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, objectId, 0, 0, 0, 0));
            }
        }

        public void ClickObject(TVpObject vpObject, TAvatar avatar)
        {
            lock (this)
            {
                ClickObject(vpObject.Id, avatar.Session);
            }
        }

        public void ClickObject(TVpObject vpObject, TAvatar avatar, Vector3 worldHit)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, vpObject.Id, avatar.Session, (float)worldHit.X, (float)worldHit.Y, (float)worldHit.Z));
            }
        }

        public void ClickObject(TVpObject vpObject, Vector3 worldHit)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, vpObject.Id, 0, (float)worldHit.X, (float)worldHit.Y, (float)worldHit.Z));
            }
        }

        public void ClickObject(int objectId,int toSession, double worldHitX, double worldHitY, double worldHitZ)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, objectId, toSession, (float)worldHitX, (float)worldHitY, (float)worldHitZ));
            }
        }

        public void ClickObject(int objectId, double worldHitX, double worldHitY, double worldHitZ)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, objectId, 0, (float)worldHitX, (float)worldHitY, (float)worldHitZ));
            }
        }


        public void ClickObject(int objectId, int toSession)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_object_click(_instance, objectId, toSession, 0, 0, 0));
            }
        }

        virtual public Task DeleteObjectAsync(TVpObject vpObject)
        {
            var referenceNumber = ObjectReferenceCounter.GetNextReference();
            var tcs = new TaskCompletionSource<object>();
            lock (this)
            {
                _objectCompletionSources.Add(referenceNumber, tcs);
                Functions.vp_int_set(_instance, IntegerAttribute.ReferenceNumber, referenceNumber);

                int rc = Functions.vp_object_delete(_instance,vpObject.Id);
                if (rc != 0)
                {
                    _objectCompletionSources.Remove(referenceNumber);
                    throw new VpException((ReasonCode)rc);
                }
            }

            return tcs.Task;
        }

        virtual public async Task<int> LoadObjectAsync(TVpObject vpObject)
        {
            var referenceNumber = ObjectReferenceCounter.GetNextReference();
            var tcs = new TaskCompletionSource<object>();
            lock (this)
            {
                _objectCompletionSources.Add(referenceNumber, tcs);
                Functions.vp_int_set(_instance, IntegerAttribute.ReferenceNumber, referenceNumber);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectId, vpObject.Id);
                Functions.vp_string_set(_instance, StringAttribute.ObjectAction, vpObject.Action);
                Functions.vp_string_set(_instance, StringAttribute.ObjectDescription, vpObject.Description);
                Functions.vp_string_set(_instance, StringAttribute.ObjectModel, vpObject.Model);
                Functions.SetData(_instance, DataAttribute.ObjectData, vpObject.Data);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationX, vpObject.Rotation.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationY, vpObject.Rotation.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationZ, vpObject.Rotation.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectX, vpObject.Position.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectY, vpObject.Position.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectZ, vpObject.Position.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationAngle, vpObject.Angle);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectType, vpObject.ObjectType);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectUserId, vpObject.Owner);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectTime, (int)new DateTimeOffset(vpObject.Time).ToUnixTimeSeconds());

                int rc = Functions.vp_object_load(_instance);
                if (rc != 0)
                {
                    _objectCompletionSources.Remove(referenceNumber);
                    throw new VpException((ReasonCode)rc);
                }
            }

            var id = (int)await tcs.Task.ConfigureAwait(false);
            vpObject.Id = id;

            return id;
        }

        virtual public async Task<int> AddObjectAsync(TVpObject vpObject)
        {
            var referenceNumber = ObjectReferenceCounter.GetNextReference();
            var tcs = new TaskCompletionSource<object>();
            lock (this)
            {
                _objectCompletionSources.Add(referenceNumber, tcs);
                Functions.vp_int_set(_instance, IntegerAttribute.ReferenceNumber, referenceNumber);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectId, vpObject.Id);
                Functions.vp_string_set(_instance, StringAttribute.ObjectAction, vpObject.Action);
                Functions.vp_string_set(_instance, StringAttribute.ObjectDescription, vpObject.Description);
                Functions.vp_string_set(_instance, StringAttribute.ObjectModel, vpObject.Model);
                Functions.SetData(_instance, DataAttribute.ObjectData, vpObject.Data);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationX, vpObject.Rotation.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationY, vpObject.Rotation.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationZ, vpObject.Rotation.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectX, vpObject.Position.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectY, vpObject.Position.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectZ, vpObject.Position.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationAngle, vpObject.Angle);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectType, vpObject.ObjectType);

                int rc = Functions.vp_object_add(_instance);
                if (rc != 0)
                {
                    _objectCompletionSources.Remove(referenceNumber);
                    throw new VpException((ReasonCode)rc);
                }
            }

            var id = (int)await tcs.Task.ConfigureAwait(false);
            vpObject.Id = id;

            return id;
        }

        virtual public Task ChangeObjectAsync(TVpObject vpObject)
        {
            var referenceNumber = ObjectReferenceCounter.GetNextReference();
            var tcs = new TaskCompletionSource<object>();
            lock (this)
            {
                _objectCompletionSources.Add(referenceNumber, tcs);
                Functions.vp_int_set(_instance, IntegerAttribute.ReferenceNumber, referenceNumber);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectId, vpObject.Id);
                Functions.vp_string_set(_instance, StringAttribute.ObjectAction, vpObject.Action);
                Functions.vp_string_set(_instance, StringAttribute.ObjectDescription, vpObject.Description);
                Functions.vp_string_set(_instance, StringAttribute.ObjectModel, vpObject.Model);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationX, vpObject.Rotation.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationY, vpObject.Rotation.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationZ, vpObject.Rotation.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectX, vpObject.Position.X);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectY, vpObject.Position.Y);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectZ, vpObject.Position.Z);
                Functions.vp_double_set(_instance, FloatAttribute.ObjectRotationAngle, vpObject.Angle);
                Functions.vp_int_set(_instance, IntegerAttribute.ObjectType, vpObject.ObjectType);

                int rc = Functions.vp_object_change(_instance);
                if (rc != 0)
                {
                    _objectCompletionSources.Remove(referenceNumber);
                    throw new VpException((ReasonCode)rc);
                }
            }


            return tcs.Task;
        }

        virtual public async Task<TVpObject> GetObjectAsync(int id)
        {
            var referenceNumber = ObjectReferenceCounter.GetNextReference();
            var tcs = new TaskCompletionSource<object>();
            lock (this)
            {
                _objectCompletionSources.Add(referenceNumber, tcs);
                Functions.vp_int_set(_instance, IntegerAttribute.ReferenceNumber, referenceNumber);
                var rc = Functions.vp_object_get(_instance, id);
                if (rc != 0)
                {
                    _objectCompletionSources.Remove(referenceNumber);
                    throw new VpException((ReasonCode)rc);
                }
            }

            var obj = (TVpObject)await tcs.Task.ConfigureAwait(false);
            return obj;
        }

        #endregion

        #region ITeleportFunctions Implementations

        virtual public void TeleportAvatar(int targetSession, string world, double x, double y, double z, double yaw, double pitch)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_teleport_avatar(_instance, targetSession, world, (float)x, (float)y,
                                                             (float)z, (float)yaw, (float)pitch));
            }
        }

        virtual public void TeleportAvatar(TAvatar avatar, string world, double x, double y, double z, double yaw, double pitch)
        {
            TeleportAvatar(avatar.Session, world, (float)x, (float)y, (float)z, (float)yaw, (float)pitch);
        }

        virtual public void TeleportAvatar(TAvatar avatar, string world, Vector3 position, double yaw, double pitch)
        {
            TeleportAvatar(avatar.Session, world, (float)position.X, (float)position.Y, (float)position.Z, (float)yaw, (float)pitch);
        }

        virtual public void TeleportAvatar(int targetSession, string world, Vector3 position, double yaw, double pitch)
        {
            TeleportAvatar(targetSession, world, (float)position.X, (float)position.Y, (float)position.Z, (float)yaw, (float)pitch);

        }

        virtual public void TeleportAvatar(TAvatar avatar, string world, Vector3 position, Vector3 rotation)
        {
            TeleportAvatar(avatar.Session, world, (float)position.X, (float)position.Y, (float)position.Z,
                           (float)rotation.Y, (float)rotation.X);
        }

        public void TeleportAvatar(TAvatar avatar, TWorld world, Vector3 position, Vector3 rotation)
        {
            TeleportAvatar(avatar.Session, world.Name, (float)position.X, (float)position.Y, (float)position.Z,
                           (float)rotation.Y, (float)rotation.X);
        }

        virtual public void TeleportAvatar(TAvatar avatar, Vector3 position, Vector3 rotation)
        {
            TeleportAvatar(avatar.Session, string.Empty, (float)position.X, (float)position.Y, (float)position.Z,
                           (float)rotation.Y, (float)rotation.X);
        }

        virtual public void TeleportAvatar(TAvatar avatar)
        {
            TeleportAvatar(avatar.Session, string.Empty, (float)avatar.Position.X, (float)avatar.Position.Y,
                           (float)avatar.Position.Z, (float)avatar.Rotation.Y, (float)avatar.Rotation.X);
        }

        #endregion

        #region IAvatarFunctions Implementations.

        virtual public void GetUserProfile(int userId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_user_attributes_by_id(_instance, userId));
            }
        }

        [Obsolete]
        virtual public void GetUserProfile(string userName)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_user_attributes_by_name(_instance, userName));
            }
        }

        virtual public void GetUserProfile(TAvatar profile)
        {
            GetUserProfile(profile.UserId);
        }

        virtual public void UpdateAvatar(double x = 0.0f, double y = 0.0f, double z = 0.0f,double yaw = 0.0f, double pitch = 0.0f)
        {
            lock (this)
            {
                Functions.vp_double_set(_instance, FloatAttribute.MyX, x);
                Functions.vp_double_set(_instance, FloatAttribute.MyY, y);
                Functions.vp_double_set(_instance, FloatAttribute.MyZ, z);
                Functions.vp_double_set(_instance, FloatAttribute.MyYaw, yaw);
                Functions.vp_double_set(_instance, FloatAttribute.MyPitch, pitch);
                CheckReasonCode(Functions.vp_state_change(_instance));

            }
        }

        public void UpdateAvatar(Vector3 position)
        {
            UpdateAvatar(position.X, position.Y, position.Z);
        }

        public void UpdateAvatar(Vector3 position, Vector3 rotation)
        {
            UpdateAvatar(position.X, position.Y, position.Z, rotation.X, rotation.Y);
        }

        public void AvatarClick(int session)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_avatar_click(_instance, session));
            }
        }

        public void AvatarClick(TAvatar avatar)
        {
            AvatarClick(avatar.Session);
        }

        #endregion

        #region IChatFunctions Implementations

        virtual public void Say(string message)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_say(_instance, message));
            }
        }

        virtual public void Say(string format, params object[] arg)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_say(_instance, string.Format(format, arg)));
            }
        }

        public void ConsoleMessage(int targetSession, string name, string message, TextEffectTypes effects = (TextEffectTypes) 0, byte red = 0, byte green = 0, byte blue = 0)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_console_message(_instance, targetSession, name, message, (int)effects, red, green, blue));
            }
        }

        public void ConsoleMessage(TAvatar avatar, string name, string message, Color color, TextEffectTypes effects = (TextEffectTypes) 0)
        {
            color = color ?? new Color();
            ConsoleMessage(avatar.Session, name, message, effects, color.R, color.G, color.B);
        }

        public void ConsoleMessage(int targetSession, string name, string message, Color color, TextEffectTypes effects = (TextEffectTypes) 0)
        {
            color = color ?? new Color();
            ConsoleMessage(targetSession, name, message, effects, color.R, color.G, color.B);
        }

        public void ConsoleMessage(string name, string message, Color color, TextEffectTypes effects = (TextEffectTypes)0)
        {
            color = color ?? new Color();
            ConsoleMessage(0, name, message, effects, color.R, color.G, color.B);
        }

        public void ConsoleMessage(string message, Color color, TextEffectTypes effects = (TextEffectTypes)0)
        {
            color = color ?? new Color();
            ConsoleMessage(0, string.Empty, message, effects, color.R, color.G, color.B);
        }

        public void ConsoleMessage(string message)
        {
            ConsoleMessage(0, string.Empty, message, 0, 0, 0, 0);
        }

        virtual public void ConsoleMessage(TAvatar avatar, string name, string message, TextEffectTypes effects = 0, byte red = 0, byte green = 0, byte blue = 0)
        {
            ConsoleMessage(avatar.Session, name, message, effects, red, green, blue);
        }

        virtual public void UrlSendOverlay(TAvatar avatar, string url)
        {
            UrlSendOverlay(avatar.Session, url);
        }

        virtual public void UrlSendOverlay(TAvatar avatar, Uri url)
        {
            UrlSendOverlay(avatar.Session, url.AbsoluteUri);
        }

        virtual public void UrlSendOverlay(int avatarSession, string url)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_url_send(_instance, avatarSession, url, (int)UrlTarget.UrlTargetOverlay));
            }
        }

        virtual public void UrlSendOverlay(int avatarSession, Uri url)
        {
            UrlSendOverlay(avatarSession, url.AbsoluteUri);
        }

        virtual public void UrlSend(TAvatar avatar, string url)
        {
            UrlSend(avatar.Session, url);
        }

        virtual public void UrlSend(TAvatar avatar, Uri url)
        {
            UrlSend(avatar.Session, url.AbsoluteUri);
        }

        virtual public void UrlSend(int avatarSession, string url)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_url_send(_instance, avatarSession, url, (int)UrlTarget.UrlTargetBrowser));
            }
        }

        virtual public void UrlSend(int avatarSession, Uri url)
        {
            UrlSend(avatarSession, url.AbsoluteUri);
        }

        #endregion

        #region IJoinFunctions Implementations
        public virtual void Join(TAvatar avatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_join(_instance, avatar.UserId));
            }
        }

        public virtual void Join(int userId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_join(_instance, userId));
            }
        }


        public virtual void JoinAccept(int requestId, string world, Vector3 location, float yaw, float pitch)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_join_accept(_instance, requestId, world,location.X,location.Y,location.Z,yaw,pitch));
            }
        }

        public virtual void JoinAccept(int requestId, string world, double x, double y, double z, float yaw, float pitch)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_join_accept(_instance, requestId, world, x, y, z, yaw, pitch));
            }
        }

        public virtual void JoinDecline(int requestId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_join_decline(_instance, requestId));
            }
        }

        #endregion

        #region  IWorldPermissionFunctions Implementations

        public virtual void WorldPermissionUser(string permission, int userId, int enable)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, permission, userId, enable));
            }
        }

        public virtual void WorldPermissionUserEnable(WorldPermissions permission, TAvatar avatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission),avatar.UserId,1));
            }
        }

        public virtual void WorldPermissionUserEnable(WorldPermissions permission, int userId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), userId, 1));
            }
        }

        public virtual void WorldPermissionUserDisable(WorldPermissions permission, TAvatar avatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), avatar.UserId, 0));
            }
        }

        public virtual void WorldPermissionUserDisable(WorldPermissions permission, int userId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), userId, 0));
            }
        }

        public virtual void WorldPermissionSession(string permission, int sessionId, int enable)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_session_set(_instance, permission, sessionId, enable));
            }
        }

        public virtual void WorldPermissionSessionEnable(WorldPermissions permission, TAvatar avatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), avatar.Session, 1));
            }
        }

        public virtual void WorldPermissionSessionEnable(WorldPermissions permission, int session)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), session, 1));
            }
        }


        public virtual void WorldPermissionSessionDisable(WorldPermissions permission, TAvatar avatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), avatar.Session, 0));
            }
        }

        public virtual void WorldPermissionSessionDisable(WorldPermissions permission, int session)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_permission_user_set(_instance, Enum.GetName(typeof(WorldPermissions), permission), session, 0));
            }
        }

        #endregion

        #region IWorldSettingsFunctions Implementations

        public virtual void WorldSettingSession(string setting, string value, TAvatar toAvatar)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_setting_set(_instance, setting, value, toAvatar.Session));
            }
        }

        public virtual void WorldSettingSession(string setting, string value, int  toSession)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_world_setting_set(_instance, setting, value, toSession));
            }
        }

        #endregion

        #endregion

        #region Events

        private readonly Dictionary<Events, EventDelegate> _nativeEvents = new Dictionary<Events, EventDelegate>();
        private readonly Dictionary<Callbacks, CallbackDelegate> _nativeCallbacks = new Dictionary<Callbacks, CallbackDelegate>();





        private void SetNativeEvent(Events eventType, EventDelegate eventFunction)
        {
            _nativeEvents[eventType] = eventFunction;
            Functions.vp_event_set(_instance, (int)eventType, eventFunction);
        }

        private void SetNativeCallback(Callbacks callbackType, CallbackDelegate callbackFunction)
        {
            _nativeCallbacks[callbackType] = callbackFunction;
            Functions.vp_callback_set(_instance, (int)callbackType, callbackFunction);
        }

        //public delegate void Event(T sender);
        public delegate void ChatMessageDelegate(T sender, ChatMessageEventArgsT<TAvatar, TChatMessage> args);

        public delegate void AvatarChangeDelegate(T sender, AvatarChangeEventArgsT<TAvatar> args);
        public delegate void AvatarEnterDelegate(T sender, AvatarEnterEventArgsT<TAvatar> args);
        public delegate void AvatarLeaveDelegate(T sender, AvatarLeaveEventArgsT<TAvatar> args);
        public delegate void AvatarClickDelegate(T sender, AvatarClickEventArgsT<TAvatar> args);

        public delegate void TeleportDelegate(T sender, TeleportEventArgsT<TTeleport, TWorld, TAvatar> args);

        public delegate void UserAttributesDelegate(T sender, UserAttributesEventArgsT<TUserAttributes> args);

        public delegate void WorldListEventDelegate(T sender, WorldListEventArgsT<TWorld> args);

        public delegate void ObjectCreateDelegate(T sender, ObjectCreateArgsT<TAvatar, TVpObject> args);
        public delegate void ObjectChangeDelegate(T sender, ObjectChangeArgsT<TAvatar, TVpObject> args);
        public delegate void ObjectDeleteDelegate(T sender, ObjectDeleteArgsT<TAvatar, TVpObject> args);
        public delegate void ObjectClickDelegate(T sender, ObjectClickArgsT<TAvatar, TVpObject> args);
        public delegate void ObjectBumpDelegate(T sender, ObjectBumpArgsT<TAvatar, TVpObject> args);

        public delegate void QueryCellResultDelegate(T sender, QueryCellResultArgsT<TVpObject> args);
        public delegate void QueryCellEndDelegate(T sender, QueryCellEndArgsT<TCell> args);

        public delegate void WorldSettingsChangedDelegate(T sender, WorldSettingsChangedEventArgsT<TWorld> args);
        public delegate void WorldDisconnectDelegate(T sender, WorldDisconnectEventArgsT<TWorld> args);

        public delegate void UniverseDisconnectDelegate(T sender, UniverseDisconnectEventArgsT<TUniverse> args);
        public delegate void JoinDelegate(T sender, JoinEventArgsT args);

        public delegate void FriendAddCallbackDelegate(T sender, FriendAddCallbackEventArgsT<TFriend> args);
        public delegate void FriendDeleteCallbackDelegate(T sender, FriendDeleteCallbackEventArgsT<TFriend> args);
        public delegate void FriendsGetCallbackDelegate(T sender, FriendsGetCallbackEventArgsT<TFriend> args);

        public event ChatMessageDelegate OnChatMessage;
        public event AvatarEnterDelegate OnAvatarEnter;
        public event AvatarChangeDelegate OnAvatarChange;
        public event AvatarLeaveDelegate OnAvatarLeave;
        public event AvatarClickDelegate OnAvatarClick;
        public event JoinDelegate OnJoin;

        public event TeleportDelegate OnTeleport;
        public event UserAttributesDelegate OnUserAttributes;

        public event ObjectCreateDelegate OnObjectCreate;
        public event ObjectChangeDelegate OnObjectChange;
        public event ObjectDeleteDelegate OnObjectDelete;
        public event ObjectClickDelegate OnObjectClick;
        public event ObjectBumpDelegate OnObjectBump;


        public event WorldListEventDelegate OnWorldList;
        public event WorldSettingsChangedDelegate OnWorldSettingsChanged;
        public event FriendAddCallbackDelegate OnFriendAddCallback;
        public event FriendDeleteCallbackDelegate OnFriendDeleteCallback;
        public event FriendsGetCallbackDelegate OnFriendsGetCallback;

        public event WorldDisconnectDelegate OnWorldDisconnect;
        public event UniverseDisconnectDelegate OnUniverseDisconnect;

        public event QueryCellResultDelegate OnQueryCellResult;
        public event QueryCellEndDelegate OnQueryCellEnd;

        /* Events indirectly assosicated with VP */

        public delegate void WorldEnterDelegate(T sender, WorldEnterEventArgsT<TWorld> args);
        public event WorldEnterDelegate OnWorldEnter;
        public delegate void WorldLeaveDelegate(T sender, WorldLeaveEventArgsT<TWorld> args);
        public event WorldLeaveDelegate OnWorldLeave;

        #endregion

        #region CallbackHandlers

        private void OnObjectCreateCallbackNative(IntPtr sender, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(reference, rc, Functions.vp_int(sender, IntegerAttribute.ObjectId));
            }
        }

        private void OnObjectChangeCallbackNative(IntPtr sender, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(reference, rc, null);
            }
        }

        private void OnObjectDeleteCallbackNative(IntPtr sender, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(reference, rc, null);
            }
        }

        void OnObjectGetCallbackNative(IntPtr sender, int rc, int reference)
        {
            lock (this)
            {
                TVpObject vpObject;
                GetVpObject(sender, out vpObject);

                SetCompletionResult(reference, rc, vpObject);
            }
        }

        private void OnObjectLoadCallbackNative(IntPtr sender, int rc, int reference)
        {
            lock (this)
            {
                SetCompletionResult(reference, rc, Functions.vp_int(sender, IntegerAttribute.ObjectId));
            }
        }
        #endregion

        #region Event handlers

        private void OnUserAttributesNative(IntPtr sender)
        {
            if (OnUserAttributes == null)
                return;
            TUserAttributes att;
            lock (this)
            {
                att = new TUserAttributes()
                          {
                              Email = Functions.vp_string(sender, StringAttribute.UserEmail),
                              Id = Functions.vp_int(sender, IntegerAttribute.UserId),
                              LastLogin = DateTimeOffset.FromUnixTimeSeconds(Functions.vp_int(sender, IntegerAttribute.UserLastLogin)).UtcDateTime,
                              Name = Functions.vp_string(sender, StringAttribute.UserName),
                              OnlineTime = new TimeSpan(0, 0, 0, Functions.vp_int(sender, IntegerAttribute.UserOnlineTime)),
                              RegistrationDate = DateTimeOffset.FromUnixTimeSeconds(Functions.vp_int(sender, IntegerAttribute.UserRegistrationTime)).UtcDateTime
                          };
            }
            OnUserAttributes(Implementor,new UserAttributesEventArgsT<TUserAttributes>(){UserAttributes = att});
        }

        private void OnTeleportNative(IntPtr sender)
        {
            if (OnTeleport == null)
                return;
            TTeleport teleport;
            lock (this)
            {
                teleport = new TTeleport
                    {
                        Avatar = GetAvatar(Functions.vp_int(sender, IntegerAttribute.AvatarSession)),
                        Position = new Vector3
                            {
                                X = Functions.vp_double(sender, FloatAttribute.TeleportX),
                                Y = Functions.vp_double(sender, FloatAttribute.TeleportY),
                                Z = Functions.vp_double(sender, FloatAttribute.TeleportZ)
                            },
                        Rotation = new Vector3
                            {
                                X = Functions.vp_double(sender, FloatAttribute.TeleportPitch),
                                Y = Functions.vp_double(sender, FloatAttribute.TeleportYaw),
                                Z = 0 /* Roll not implemented yet */
                            },
                            // TODO: maintain user count and world state statistics.
                        World = new TWorld { Name = Functions.vp_string(sender, StringAttribute.TeleportWorld),State = WorldState.Unknown, UserCount=-1 }
                    };
            }
            OnTeleport(Implementor, new TeleportEventArgsT<TTeleport, TWorld, TAvatar>{Teleport = teleport});
        }

        private void OnGetFriendsCallbackNative(IntPtr sender, int rc, int reference)
        {
            if (OnFriendAddCallback == null)
                return;
            lock (this)
            {
                var friend = new TFriend
                    {
                        UserId = Functions.vp_int(sender,IntegerAttribute.UserId),
                        Id = Functions.vp_int(sender,IntegerAttribute.FriendId),
                        Name = Functions.vp_string(sender, StringAttribute.FriendName),
                       Online = Functions.vp_int(sender,IntegerAttribute.FriendOnline)==1
                    };
                OnFriendsGetCallback(Implementor, new FriendsGetCallbackEventArgsT<TFriend> { Friend = friend });
            }
        }

        private void OnFriendDeleteCallbackNative(IntPtr sender, int rc, int reference)
        {
            // todo: implement this.
        }

        private void OnFriendAddCallbackNative(IntPtr sender, int rc, int reference)
        {
            // todo: implement this.
        }


        private void OnChatNative(IntPtr sender)
        {
            ChatMessageEventArgsT<TAvatar, TChatMessage> data;
            lock (this)
            {
                if (!_avatars.ContainsKey(Functions.vp_int(sender, IntegerAttribute.AvatarSession)))
                {
                    var avatar = new TAvatar
                        {
                            Name = Functions.vp_string(sender, StringAttribute.AvatarName),
                            Session = Functions.vp_int(sender, IntegerAttribute.AvatarSession)
                        };
                    _avatars.Add(avatar.Session, avatar);
                }
                data = new ChatMessageEventArgsT<TAvatar, TChatMessage>
                {
                    Avatar = _avatars[Functions.vp_int(sender, IntegerAttribute.AvatarSession)],
                    ChatMessage = new TChatMessage
                    {
                        Type = (ChatMessageTypes)Functions.vp_int(sender, IntegerAttribute.ChatType),
                        Message = Functions.vp_string(sender, StringAttribute.ChatMessage),
                        Name = Functions.vp_string(sender, StringAttribute.AvatarName),
                        TextEffectTypes = (TextEffectTypes)Functions.vp_int(sender, IntegerAttribute.ChatEffects)
                    }
                };
                OperatingSystem os = Environment.OSVersion;

                if (data.ChatMessage.Message == string.Format("{0} version", Configuration.BotName) && !HasParentInstance )
                {
                    Version assemblyVersion = Assembly.GetAssembly(typeof(Functions)).GetName().Version;

                    ConsoleMessage(data.Avatar, "VPNET",
                        $"Version {assemblyVersion} running on {os.VersionString}", 
                        TextEffectTypes.Bold, 0, 0, 127);
                }

                if (OnChatMessage == null) return;
                if (data.ChatMessage.Type == ChatMessageTypes.Console)
                {
                    data.ChatMessage.Color = new Color
                    {
                        R = (byte)Functions.vp_int(sender, IntegerAttribute.ChatRolorRed),
                        G = (byte)Functions.vp_int(sender, IntegerAttribute.ChatColorGreen),
                        B = (byte)Functions.vp_int(sender, IntegerAttribute.ChatColorBlue)
                    };
                }
                else
                {
                    data.ChatMessage.Color = new Color
                    {
                        R = 0,
                        G = 0,
                        B = 0
                    };
                }
            }
            OnChatMessage(Implementor, data);
        }

        private void OnAvatarAddNative(IntPtr sender)
        {
            TAvatar data;
            lock (this)
            {
                data = new TAvatar
                {
                    UserId = Functions.vp_int(sender, IntegerAttribute.UserId),
                    Name = Functions.vp_string(sender, StringAttribute.AvatarName),
                    Session = Functions.vp_int(sender, IntegerAttribute.AvatarSession),
                    AvatarType = Functions.vp_int(sender, IntegerAttribute.AvatarType),
                    Position = new Vector3
                    {
                        X = Functions.vp_double(sender, FloatAttribute.AvatarX),
                        Y = Functions.vp_double(sender, FloatAttribute.AvatarY),
                        Z = Functions.vp_double(sender, FloatAttribute.AvatarZ)
                    },
                    Rotation = new Vector3
                    {
                        X = Functions.vp_double(sender, FloatAttribute.AvatarPitch),
                        Y = Functions.vp_double(sender, FloatAttribute.AvatarYaw),
                        Z = 0 /* roll currently not supported*/
                    }
                };
                if (!_avatars.ContainsKey(data.Session))
                    _avatars.Add(data.Session, data);
            }
            data.LastChanged = DateTime.UtcNow;
            if (OnAvatarEnter == null) return;
            var args = new AvatarEnterEventArgsT<TAvatar> {Avatar = data, Implementor = Implementor};
            args.Initialize();
            OnAvatarEnter(Implementor, args);
        }

        private void OnAvatarChangeNative(IntPtr sender)
        {
            TAvatar old;
            TAvatar data;
            lock (this)
            {
                data = new TAvatar
                {
                    UserId = _avatars[Functions.vp_int(sender, IntegerAttribute.AvatarSession)].UserId,
                    Name = Functions.vp_string(sender, StringAttribute.AvatarName),
                    Session = Functions.vp_int(sender, IntegerAttribute.AvatarSession),
                    AvatarType = Functions.vp_int(sender, IntegerAttribute.AvatarType),
                    Position = new Vector3
                    {
                        X = Functions.vp_double(sender, FloatAttribute.AvatarX),
                        Y = Functions.vp_double(sender, FloatAttribute.AvatarY),
                        Z = Functions.vp_double(sender, FloatAttribute.AvatarZ)
                    },
                    Rotation = new Vector3
                    {
                        X = Functions.vp_double(sender, FloatAttribute.AvatarPitch),
                        Y = Functions.vp_double(sender, FloatAttribute.AvatarYaw),
                        Z = 0 /* roll currently not supported*/
                    }
                };
                // determine if the avatar actually changed.
                old = new TAvatar().CopyFrom(_avatars[data.Session], true);
                if (data.Position.X == old.Position.X
                    && data.Position.Y == old.Position.Y
                    && data.Position.Z == old.Position.Z
                    && data.Rotation.X == old.Rotation.X
                    && data.Rotation.Y == old.Rotation.Y
                    && data.Rotation.Z == old.Rotation.Z)
                    return;
                data.LastChanged = DateTime.UtcNow;
                setAvatar(data);

            }
            if (OnAvatarChange != null)
                OnAvatarChange(Implementor, new AvatarChangeEventArgsT<TAvatar> { Avatar = _avatars[data.Session], AvatarPrevious = old });
        }

        private void OnAvatarDeleteNative(IntPtr sender)
        {
            TAvatar data;
            lock (this)
            {
                try
                {
                    data = _avatars[Functions.vp_int(sender, IntegerAttribute.AvatarSession)];
                    _avatars.Remove(data.Session);
                    if (OnAvatarLeave == null) return;
                    OnAvatarLeave(Implementor, new AvatarLeaveEventArgsT<TAvatar> { Avatar = data });
                }
                catch
                {

                }
            }
        }

        private void OnAvatarClickNative(IntPtr sender)
        {
            if (OnAvatarClick == null) return;
            lock (this)
            {
                var clickedAvatar = Functions.vp_int(sender, IntegerAttribute.ClickedSession);
                if (clickedAvatar == 0)
                    clickedAvatar = Functions.vp_int(sender, IntegerAttribute.AvatarSession);

                OnAvatarClick(Implementor,
                    new AvatarClickEventArgsT<TAvatar>
                    {
                        Avatar = GetAvatar(Functions.vp_int(sender, IntegerAttribute.AvatarSession)),
                        ClickedAvatar = GetAvatar(clickedAvatar),
                        WorldHit = new Vector3
                        {
                            X = Functions.vp_double(sender, FloatAttribute.ClickHitX),
                            Y = Functions.vp_double(sender, FloatAttribute.ClickHitY),
                            Z = Functions.vp_double(sender, FloatAttribute.ClickHitZ)
                        }
                    });
            }
        }

        private void OnObjectClickNative(IntPtr sender)
        {
            if (OnObjectClick == null) return;
            int session;
            int objectId;
            Vector3 world;
            lock (this)
            {
                session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                objectId = Functions.vp_int(sender, IntegerAttribute.ObjectId);
                world = new Vector3
                    {
                        X = Functions.vp_double(sender, FloatAttribute.ClickHitX),
                        Y = Functions.vp_double(sender, FloatAttribute.ClickHitY),
                        Z = Functions.vp_double(sender, FloatAttribute.ClickHitZ)
                    };
            }

            OnObjectClick(Implementor,
                          new ObjectClickArgsT<TAvatar,TVpObject>
                              {WorldHit=world, Avatar = GetAvatar(session), VpObject = new TVpObject {Id = objectId}});
        }

        private void OnObjectBumpNative(IntPtr sender)
        {
            if (OnObjectBump == null) return;
            int session;
            int objectId;
            Vector3 world;
            lock (this)
            {
                session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                objectId = Functions.vp_int(sender, IntegerAttribute.ObjectId);
            }

            OnObjectBump(Implementor,
                          new ObjectBumpArgsT<TAvatar, TVpObject> { BumpType = BumpType.BumpBegin, Avatar = GetAvatar(session), VpObject = new TVpObject { Id = objectId } });
        }

        private void OnObjectBumpEndNative(IntPtr sender)
        {
            if (OnObjectBump == null) return;
            int session;
            int objectId;
            Vector3 world;
            lock (this)
            {
                session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                objectId = Functions.vp_int(sender, IntegerAttribute.ObjectId);
            }

            OnObjectBump(Implementor,
                          new ObjectBumpArgsT<TAvatar, TVpObject> { BumpType = BumpType.BumpEnd, Avatar = GetAvatar(session), VpObject = new TVpObject { Id = objectId } });
        }

        private void OnObjectDeleteNative(IntPtr sender)
        {
            if (OnObjectDelete == null) return;
            int session;
            int objectId;
            lock (this)
            {
                session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                objectId = Functions.vp_int(sender, IntegerAttribute.ObjectId);
            }
            OnObjectDelete(Implementor, new ObjectDeleteArgsT<TAvatar, TVpObject> { Avatar = GetAvatar(session), VpObject = new TVpObject { Id = objectId } });
        }

        private void OnObjectCreateNative(IntPtr sender)
        {
            if (OnObjectCreate == null && OnQueryCellResult == null) return;
            TVpObject vpObject;
            int session;
            lock (this)
            {
                session = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
                GetVpObject(sender, out vpObject);
            }
            if (session == 0 && OnQueryCellResult != null)
                OnQueryCellResult(Implementor, new QueryCellResultArgsT<TVpObject> { VpObject = vpObject });
            else
                if (OnObjectCreate != null)
                OnObjectCreate(Implementor, new ObjectCreateArgsT<TAvatar, TVpObject> { Avatar = GetAvatar(session), VpObject = vpObject });
        }



        public List<TAvatar> Avatars()
        {
            return _avatars.Values.ToList();
        }

        [Obsolete("Objects are not firewalled anymore, so commits are not needed.")]
        public void Commit(TAvatar avatar)
        {
            //lock (_avatars)
            //{
            //    var cache = _avatars[avatar.Session];
            //    cache.CopyFrom(avatar, false);
            //    foreach (var prop in cache.GetType().GetFields())
            //    {
            //        if (prop.FieldType.BaseType!=null && prop.FieldType.BaseType.Name.StartsWith("BaseInstanceT"))
            //            prop.SetValue(cache, this);
            //    }
            //    _avatars[avatar.Session] = cache;
            //}
        }

        public TAvatar GetAvatar(int session)
        {
            if (_avatars.ContainsKey(session))
                return _avatars[session];
            var avatar = new TAvatar { Session = session };
            _avatars.Add(session, avatar);
            return avatar;
        }

        private void setAvatar(TAvatar avatar)
        {
            lock (this)
            {
                if (_avatars.ContainsKey(avatar.Session))
                {
                    _avatars[avatar.Session].CopyFrom(avatar, true);
                }
                else
                {
                    _avatars[avatar.Session] = avatar;
                }
            }
        }

        private static void GetVpObject(IntPtr sender, out TVpObject vpObject)
        {

            vpObject = new TVpObject
            {
                Action = Functions.vp_string(sender, StringAttribute.ObjectAction),
                Description = Functions.vp_string(sender, StringAttribute.ObjectDescription),
                Id = Functions.vp_int(sender, IntegerAttribute.ObjectId),
                Model = Functions.vp_string(sender, StringAttribute.ObjectModel),
                Data = Functions.GetData(sender, DataAttribute.ObjectData),

                Rotation = new Vector3
                {
                    X = Functions.vp_double(sender, FloatAttribute.ObjectRotationX),
                    Y = Functions.vp_double(sender, FloatAttribute.ObjectRotationY),
                    Z = Functions.vp_double(sender, FloatAttribute.ObjectRotationZ)
                },

                Time = DateTimeOffset.FromUnixTimeSeconds(Functions.vp_int(sender, IntegerAttribute.ObjectTime)).UtcDateTime,
                ObjectType = Functions.vp_int(sender, IntegerAttribute.ObjectType),
                Owner = Functions.vp_int(sender, IntegerAttribute.ObjectUserId),
                Position = new Vector3
                {
                    X = Functions.vp_double(sender, FloatAttribute.ObjectX),
                    Y = Functions.vp_double(sender, FloatAttribute.ObjectY),
                    Z = Functions.vp_double(sender, FloatAttribute.ObjectZ)
                },
                Angle = Functions.vp_double(sender, FloatAttribute.ObjectRotationAngle)
            };
        }

        private void OnObjectChangeNative(IntPtr sender)
        {
            if (OnObjectChange == null) return;
            TVpObject vpObject;
            int sessionId;
            lock (this)
            {
                GetVpObject(sender,out vpObject);
                sessionId = Functions.vp_int(sender, IntegerAttribute.AvatarSession);
            }
            OnObjectChange(Implementor, new ObjectChangeArgsT<TAvatar, TVpObject> { Avatar = GetAvatar(sessionId), VpObject = vpObject });
        }

        private void OnQueryCellEndNative(IntPtr sender)
        {
            if (OnQueryCellEnd == null) return;
            int x;
            int z;
            lock (this)
            {
                x = Functions.vp_int(sender, IntegerAttribute.CellX);
                z = Functions.vp_int(sender, IntegerAttribute.CellZ);
            }
            OnQueryCellEnd(Implementor, new QueryCellEndArgsT<TCell> { Cell = new TCell { X = x, Z = z } });
        }

        private void OnWorldListNative(IntPtr sender)
        {
            if (OnWorldList == null)
                return;

            TWorld data;
            lock (this)
            {
                string worldName = Functions.vp_string(_instance, StringAttribute.WorldName);
                data = new TWorld
                {
                    Name = worldName,
                    State = (WorldState)Functions.vp_int(_instance, IntegerAttribute.WorldState),
                    UserCount = Functions.vp_int(_instance, IntegerAttribute.WorldUsers)
                };
            }
            if (_worlds.ContainsKey(data.Name))
                _worlds.Remove(data.Name);
            _worlds.Add(data.Name,data);
            OnWorldList(Implementor, new WorldListEventArgsT<TWorld> { World = data });
        }

        private void OnWorldSettingNativeEvent(IntPtr instance)
        {
            if (!_worlds.ContainsKey(Configuration.World.Name))
            {
                _worlds.Add(Configuration.World.Name,Configuration.World);
            }
            var world = _worlds[Configuration.World.Name];
            var key = Functions.vp_string(instance, StringAttribute.WorldSettingKey);
            var value = Functions.vp_string(instance, StringAttribute.WorldSettingValue);
            world.RawAttributes[key] = value;
        }

        private void OnWorldSettingsChangedNativeEvent(IntPtr instance)
        {
            // Initialize World Object Cache if a local object path has been specified and a objectpath is speficied in the world attributes.
            // TODO: some world, such as Test do not specify a objectpath, maybe there's a default search path we dont know of.
            var world = _worlds[Configuration.World.Name];
            if (!string.IsNullOrEmpty(world.LocalCachePath) && world.RawAttributes.ContainsKey("objectpath"))
            {
                ModelCacheProvider = new OpCacheProvider(_worlds[Configuration.World.Name].RawAttributes["objectpath"],world.LocalCachePath);
            }
            if (OnWorldSettingsChanged != null)
                OnWorldSettingsChanged(Implementor, new WorldSettingsChangedEventArgsT<TWorld>() { World = _worlds[Configuration.World.Name]});
        }

        private void OnUniverseDisconnectNative(IntPtr sender)
        {
            if (OnUniverseDisconnect == null) return;
            OnUniverseDisconnect(Implementor, new UniverseDisconnectEventArgsT<TUniverse> { Universe = Universe });
        }

        private void OnWorldDisconnectNative(IntPtr sender)
        {
            if (OnWorldDisconnect == null) return;
            OnWorldDisconnect(Implementor, new WorldDisconnectEventArgsT<TWorld> { World = World });
        }

        private void OnJoinNative(IntPtr sender)
        {
            if (OnJoin == null) return;
            OnJoin(Implementor, new JoinEventArgsT {
                UserId = Functions.vp_int(sender, IntegerAttribute.UserId),
                Id = Functions.vp_int(sender, IntegerAttribute.JoinId),
                Name = Functions.vp_string(sender, StringAttribute.JoinName)
            });
        }

        #endregion

        #region Cleanup

        public void ReleaseEvents()
        {
            lock (this)
            {
                OnChatMessage = null;
                OnAvatarEnter = null;
                OnAvatarChange = null;
                OnAvatarLeave = null;
                OnObjectCreate = null;
                OnObjectChange = null;
                OnObjectDelete = null;
                OnObjectClick = null;
                OnObjectBump = null;
                OnWorldList = null;
                OnWorldDisconnect = null;
                OnWorldSettingsChanged = null;
                OnWorldDisconnect = null;
                OnUniverseDisconnect = null;
                //OnUserAttributes = null;
                OnQueryCellResult = null;
                OnQueryCellEnd = null;
                OnFriendAddCallback = null;
                OnFriendDeleteCallback = null;
                OnFriendsGetCallback = null;
            }
        }

        public void Dispose()
        {
            if (_instance != IntPtr.Zero)
            {
                if (Configuration.IsChildInstance)
                    return;
                Functions.vp_destroy(_instance);
            }

            if (instanceHandle != null)
            {
                instanceHandle.Free();
            }

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Friend Functions

        public void GetFriends()
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_friends_get(_instance));
            }
        }

        public void AddFriendByName(TFriend friend)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_friend_add_by_name(_instance, friend.Name));
            }
        }

        public void AddFriendByName(string name)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_friend_add_by_name(_instance, name));
            }
        }

        public void DeleteFriendById(int friendId)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_friend_delete(_instance, friendId));
            }
        }

        public void DeleteFriendById(TFriend friend)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_friend_delete(_instance, friend.Id));
            }
        }

        #endregion

        #region ITerrainFunctions Implementation

        public void TerrianQuery(int tileX, int tileZ, int[,] nodes)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_terrain_query(_instance, tileX, tileZ, nodes));
            }
        }

        public void SetTerrainNode(int tileX, int tileZ, int nodeX, int nodeZ, TerrainCell[,] cells)
        {
            lock (this)
            {
                CheckReasonCode(Functions.vp_terrain_node_set(_instance, tileX, tileZ, nodeX, nodeZ, cells));
            }
        }

        #endregion

        #region Implementation of IInstanceEvents

        override internal event EventDelegate OnChatNativeEvent;
        override internal event EventDelegate OnAvatarAddNativeEvent;
        override internal event EventDelegate OnAvatarDeleteNativeEvent;
        override internal event EventDelegate OnAvatarChangeNativeEvent;
        override internal event EventDelegate OnAvatarClickNativeEvent;
        override internal event EventDelegate OnWorldListNativeEvent;
        override internal event EventDelegate OnObjectChangeNativeEvent;
        override internal event EventDelegate OnObjectCreateNativeEvent;
        override internal event EventDelegate OnObjectDeleteNativeEvent;
        override internal event EventDelegate OnObjectClickNativeEvent;
        override internal event EventDelegate OnObjectBumpNativeEvent;
        override internal event EventDelegate OnObjectBumpEndNativeEvent;
        override internal event EventDelegate OnQueryCellEndNativeEvent;
        override internal event EventDelegate OnUniverseDisconnectNativeEvent;
        override internal event EventDelegate OnWorldDisconnectNativeEvent;
        override internal event EventDelegate OnTeleportNativeEvent;
        override internal event EventDelegate OnUserAttributesNativeEvent;
        override internal event EventDelegate OnJoinNativeEvent;

        override internal event CallbackDelegate OnObjectCreateCallbackNativeEvent;
        override internal event CallbackDelegate OnObjectChangeCallbackNativeEvent;
        override internal event CallbackDelegate OnObjectDeleteCallbackNativeEvent;
        override internal event CallbackDelegate OnObjectGetCallbackNativeEvent;
        override internal event CallbackDelegate OnObjectLoadCallbackNativeEvent;
        override internal event CallbackDelegate OnFriendAddCallbackNativeEvent;
        override internal event CallbackDelegate OnFriendDeleteCallbackNativeEvent;
        override internal event CallbackDelegate OnGetFriendsCallbackNativeEvent;

        override internal event CallbackDelegate OnJoinCallbackNativeEvent;
        override internal event CallbackDelegate OnWorldPermissionUserSetCallbackNativeEvent;
        override internal event CallbackDelegate OnWorldPermissionSessionSetCallbackNativeEvent;
        override internal event CallbackDelegate OnWorldSettingsSetCallbackNativeEvent;


        #endregion

        #region Implementation of IAvatarFunctions<out void,TAvatar,in Vector3>

        Dictionary<int, TAvatar> IAvatarFunctions<TAvatar>.Avatars { get; set; }

        #endregion
    }
}
