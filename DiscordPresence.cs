using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord.Clients;
using Oxide.Ext.Discord.Connections;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Entities.Activities;
using Oxide.Ext.Discord.Entities.Gateway;
using Oxide.Ext.Discord.Entities.Gateway.Commands;
using Oxide.Ext.Discord.Entities.Gateway.Events;
using Oxide.Ext.Discord.Entities.Users;
using Oxide.Ext.Discord.Interfaces;
using Oxide.Ext.Discord.Libraries.Placeholders;
using Oxide.Ext.Discord.Logging;

namespace Oxide.Plugins
{
    [Info("Discord Presence", "MJSU", "3.0.0")]
    [Description("Updates the Discord bot status message")]
    internal class DiscordPresence : CovalencePlugin, IDiscordPlugin
    {
        #region Class Fields
        public DiscordClient Client { get; set; }

        private PluginConfig _pluginConfig;

        private int _index;

        private readonly DiscordPlaceholders _placeholders = GetLibrary<DiscordPlaceholders>();

        private BotConnection _discordSettings;

        private readonly UpdatePresenceCommand _command = new UpdatePresenceCommand
        {
            Afk = false,
            Since = 0,
            Status = UserStatusType.Online
        };

        private readonly DiscordActivity _activity = new DiscordActivity();

        private bool _initialized;
        #endregion

        #region Setup & Loading
        private void Init()
        {
            _discordSettings = new BotConnection
            {
                ApiToken = _pluginConfig.Token,
                LogLevel = _pluginConfig.ExtensionDebugging,
                Intents = GatewayIntents.None
            };

            _command.Activities = new List<DiscordActivity> {_activity};
            if (_pluginConfig.UpdateRate < 1f)
            {
                _pluginConfig.UpdateRate = 1f;
            }
        }

        [HookMethod(DiscordExtHooks.OnDiscordClientCreated)]
        private void OnDiscordClientCreated()
        {
            Connect();
        }
        
        private void OnServerInitialized()
        {
            _initialized = true;
        }

        public void Connect()
        {
            if (string.IsNullOrEmpty(_pluginConfig.Token))
            {
                PrintWarning("Please enter your bot token in the config and reload the plugin.");
                return;
            }

            Client.Connect(_discordSettings);
        }

        [HookMethod(DiscordExtHooks.OnDiscordGatewayReady)]
        private void OnDiscordGatewayReady(GatewayReadyEvent ready)
        {
            Puts($"{Title} Ready");
            timer.In(1f, InitialMessage);
            timer.Every(_pluginConfig.UpdateRate, UpdatePresence);
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        public PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.LoadingMessage = new MessageSettings(config.LoadingMessage ?? new MessageSettings("Server is booting", ActivityType.Game));
            config.StatusMessages = config.StatusMessages ?? new List<MessageSettings>
            {
                new MessageSettings("on {server.name}", ActivityType.Game),
                new MessageSettings("{server.players}/{server.players.max} Players", ActivityType.Game),
                new MessageSettings("{server.players.sleepers} Sleepers", ActivityType.Game),
                new MessageSettings("{server.players.stored} Total Players", ActivityType.Game),
                new MessageSettings("Server FPS {server.fps}", ActivityType.Game),
                new MessageSettings("{server.entities} Entities", ActivityType.Game),
                new MessageSettings("{server.players.total} Lifetime Players", ActivityType.Game),
                #if RUST
                new MessageSettings("{server.entities} Entities", ActivityType.Game),
                new MessageSettings("{server.players.queued} Queued", ActivityType.Game),
                new MessageSettings("{server.players.loading} Joining", ActivityType.Game),
                new MessageSettings("Wiped: {server.map.wipe.last!local}", ActivityType.Game),
                new MessageSettings("Size: {world.size} Seed: {world.seed}", ActivityType.Game)
                #endif
            };

            for (int index = 0; index < config.StatusMessages.Count; index++)
            {
                config.StatusMessages[index] = new MessageSettings(config.StatusMessages[index]);
            }

            return config;
        }

        public void InitialMessage()
        {
            if (_initialized)
            {
                UpdatePresence();
                return;
            }

            SendUpdate(_pluginConfig.LoadingMessage);
        }
        
        public void UpdatePresence()
        {
            if (_pluginConfig.StatusMessages.Count == 0)
            {
                PrintError("Presence Text formats contains no values. Please add some to your config");
                return;
            }
            
            SendUpdate(_pluginConfig.StatusMessages[_index]);
            _index = ++_index % _pluginConfig.StatusMessages.Count;
        }

        public void SendUpdate(MessageSettings settings)
        {
            _activity.Name = _placeholders.ProcessPlaceholders(settings.Message, GetDefault());
            _activity.Type = settings.Type;

            Client?.UpdateStatus(_command);
        }

        public PlaceholderData GetDefault()
        {
            return _placeholders.CreateData(this);
        }
        #endregion

        #region Classes
        public class PluginConfig
        {
            [DefaultValue("")]
            [JsonProperty(PropertyName = "Discord Application Bot Token")]
            public string Token { get; set; }
            
            [DefaultValue(15f)]
            [JsonProperty(PropertyName = "Update Rate (Seconds)")]
            public float UpdateRate { get; set; }

            [JsonProperty(PropertyName = "Presence Messages")]
            public List<MessageSettings> StatusMessages { get; set; }
            
            [JsonProperty(PropertyName = "Server Loading Message")]
            public MessageSettings LoadingMessage { get; set; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DiscordLogLevel.Info)]
            [JsonProperty(PropertyName = "Discord Extension Log Level (Verbose, Debug, Info, Warning, Error, Exception, Off)")]
            public DiscordLogLevel ExtensionDebugging { get; set; }
        }

        public class MessageSettings
        {
            public string Message { get; set; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(ActivityType.Game)]
            public ActivityType Type { get; set; }

            [JsonConstructor]
            public MessageSettings() { }
            
            public MessageSettings(string message, ActivityType type)
            {
                Message = message;
                Type = type;
            }
            
            public MessageSettings(MessageSettings settings)
            {
                Message = settings?.Message ?? string.Empty;
                Type = settings?.Type ?? ActivityType.Game;
            }
        }
        #endregion
    }
}