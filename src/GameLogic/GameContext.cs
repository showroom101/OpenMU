﻿// <copyright file="GameContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;

    using log4net;
    using MUnique.OpenMU.DataModel.Configuration;
    using MUnique.OpenMU.Persistence;

    /// <summary>
    /// The game context which holds all data of the game together.
    /// </summary>
    public class GameContext : OpenMU.GameLogic.IGameContext, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameContext));

        private readonly Timer recoverTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="GameContext"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="repositoryManager">The repository manager.</param>
        public GameContext(GameConfiguration configuration, IRepositoryManager repositoryManager)
        {
            try
            {
                this.Configuration = configuration;
                this.RepositoryManager = repositoryManager;
                this.MapList = new Dictionary<ushort, GameMap>();
                this.recoverTimer = new Timer(this.RecoverTimerElapsed, null, this.Configuration.RecoveryInterval, this.Configuration.RecoveryInterval);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public IDictionary<ushort, GameMap> MapList { get; }

        /// <inheritdoc/>
        public GameConfiguration Configuration { get; }

        /// <inheritdoc/>
        public IItemPowerUpFactory ItemPowerUpFactory { get; } = new ItemPowerUpFactory();

        /// <inheritdoc/>
        public IRepositoryManager RepositoryManager { get; }

        /// <summary>
        /// Gets the player list.
        /// </summary>
        public IList<Player> PlayerList { get; } = new List<Player>();

        /// <summary>
        /// Gets the players by character name dictionary.
        /// </summary>
        public IDictionary<string, Player> PlayersByCharacterName { get; } = new ConcurrentDictionary<string, Player>();

        /// <summary>
        /// Adds the player to the game.
        /// </summary>
        /// <param name="player">The player.</param>
        public virtual void AddPlayer(Player player)
        {
            player.PlayerLeftWorld += this.PlayerLeftWorld;
            player.PlayerEnteredWorld += this.PlayerEnteredWorld;
            player.PlayerDisconnected += this.PlayerDisconnected;
            this.PlayerList.Add(player);
        }

        /// <summary>
        /// Removes the player from the game.
        /// </summary>
        /// <param name="player">The player.</param>
        public virtual void RemovePlayer(Player player)
        {
            if (player == null)
            {
                return;
            }

            if (player.SelectedCharacter != null)
            {
                this.PlayersByCharacterName.Remove(player.SelectedCharacter.Name);
            }

            player.CurrentMap?.Remove(player);

            this.PlayerList.Remove(player);

            player.PlayerDisconnected -= this.PlayerDisconnected;
            player.PlayerEnteredWorld -= this.PlayerEnteredWorld;
            player.PlayerLeftWorld -= this.PlayerLeftWorld;
        }

        /// <summary>
        /// Gets the player by the character name.
        /// </summary>
        /// <param name="name">The character name.</param>
        /// <returns>The player by character name.</returns>
        public Player GetPlayerByCharacterName(string name)
        {
            this.PlayersByCharacterName.TryGetValue(name, out Player player);
            return player;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="managed"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "recoverTimer", Justification = "Null-conditional confuses the code analysis.")]
        protected virtual void Dispose(bool managed)
        {
            if (managed)
            {
                this.recoverTimer.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        private void RecoverTimerElapsed(object state)
        {
            for (int i = this.PlayerList.Count - 1; i >= 0; --i)
            {
                if (i < 0)
                {
                    break;
                }

                var player = this.PlayerList[i];
                if (player.SelectedCharacter != null && player.PlayerState.CurrentState == PlayerState.EnteredWorld)
                {
                    player.Regenerate();
                }
            }
        }

        private void PlayerDisconnected(object sender, EventArgs e)
        {
            var player = sender as Player;
            if (player == null)
            {
                return;
            }

            this.RemovePlayer(player);
        }

        private void PlayerEnteredWorld(object sender, EventArgs e)
        {
            var player = sender as Player;
            if (player == null)
            {
                return;
            }

            this.PlayersByCharacterName.Add(player.SelectedCharacter.Name, player);
        }

        private void PlayerLeftWorld(object sender, EventArgs e)
        {
            var player = sender as Player;
            if (player == null)
            {
                return;
            }

            this.PlayersByCharacterName.Remove(player.SelectedCharacter.Name);
        }
    }
}
