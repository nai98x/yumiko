﻿namespace Yumiko.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Not with D#+ Command classes")]
    [SlashCommandGroup("owner", "Comandos solo disponibles para el owner de Yumiko")]
    [SlashRequireOwner]
    public class Owner : ApplicationCommandModule
    {
        public IConfigurationRoot Configuration { private get; set; } = null!;

        public override Task<bool> BeforeSlashExecutionAsync(InteractionContext ctx)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ctx.Interaction.Locale!);
            return Task.FromResult(true);
        }

        [SlashCommand("test", "Testing command")]
        [DescriptionLocalization(Localization.Spanish, "Comando de pruebas")]
        public async Task Test(InteractionContext ctx)
        {
            await ctx.DeferAsync();
            //await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(String.Format(strings.test, ctx.User.Username)));
        }

        [SlashCommand("guild", "Information about a guild")]
        [NameLocalization(Localization.Spanish, "servidor")]
        [DescriptionLocalization(Localization.Spanish, "Información sobre un servidor")]
        public async Task Guild(InteractionContext ctx, [Option("guild_id", "Guild Id to see details")] string id)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            bool validId = ulong.TryParse(id, out ulong guildId);
            if (validId)
            {
                bool validGuild = ctx.Client.Guilds.TryGetValue(guildId, out DiscordGuild? guild);
                if (validGuild)
                {
                    string desc =
                        $"  - {Formatter.Bold("Id")}: {guild?.Id}\n" +
                        $"  - {Formatter.Bold(translations.joined_date)}: {guild?.JoinedAt}\n" +
                        $"  - {Formatter.Bold(translations.member_count)}: {guild?.MemberCount}\n\n";

                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = guild?.Name,
                        Description = desc,
                        Color = DiscordColor.Green,
                    }));
                }
                else
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = translations.error,
                        Description = string.Format(translations.guild_with_id_not_found, id),
                        Color = DiscordColor.Red,
                    }));
                }
            }
            else
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                {
                    Title = translations.error,
                    Description = string.Format(translations.id_not_valid, id),
                    Color = DiscordColor.Red,
                }));
            }
        }

        [SlashCommand("guilds", "See Yumiko's guilds")]
        [NameLocalization(Localization.Spanish, "servidores")]
        [DescriptionLocalization(Localization.Spanish, "Muestra los servidores de Yumiko")]
        public async Task Servers(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var interactivity = ctx.Client.GetInteractivity();
            List<Page> pages = new();
            var guildsdesordenadas = ctx.Client.Guilds.Values;
            var lista = guildsdesordenadas.ToList();
            lista.Sort((x, y) => x.JoinedAt.CompareTo(y.JoinedAt));
            string servers = string.Empty;
            int cont = 1;
            int miembros;
            foreach (var guild in lista)
            {
                if (cont >= 10)
                {
                    pages.Add(new Page()
                    {
                        Embed = new DiscordEmbedBuilder
                        {
                            Title = string.Format(translations.bot_guilds, ctx.Client.CurrentUser.Username),
                            Description = servers,
                            Color = Constants.YumikoColor,
                        },
                    });
                    cont = 1;
                    servers = string.Empty;
                }

                miembros = guild.MemberCount - 1;
                servers +=
                    $"{Formatter.Bold(guild.Name)}\n" +
                    $"  - {Formatter.Bold("Id")}: {guild.Id}\n" +
                    $"  - {Formatter.Bold(translations.joined_date)}: {guild.JoinedAt}\n" +
                    $"  - {Formatter.Bold(translations.member_count)}: {guild.MemberCount}\n\n";
                cont++;
            }

            if (cont != 1)
            {
                pages.Add(new Page()
                {
                    Embed = new DiscordEmbedBuilder
                    {
                        Title = string.Format(translations.bot_guilds, ctx.Client.CurrentUser.Username),
                        Description = servers,
                        Color = Constants.YumikoColor,
                    },
                });
            }

            await ctx.DeleteResponseAsync();
            await interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, pages, token: new CancellationTokenSource(TimeSpan.FromSeconds(300)).Token);
        }

        [SlashCommand("deleteguild", "Yumiko leaves a guild")]
        [NameLocalization(Localization.Spanish, "eliminarservidor")]
        [DescriptionLocalization(Localization.Spanish, "Yumiko deja un servidor")]
        public async Task EliminarServer(InteractionContext ctx, [Option("Id", "Guild Id to exit")] string idStr)
        {
            try
            {
                bool ok = ulong.TryParse(idStr, out ulong id);
                if (ok)
                {
                    var guild = await ctx.Client.GetGuildAsync(id);
                    string nombre = guild.Name;
                    await guild.LeaveAsync();
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(string.Format(translations.bot_left_guild, nombre, id)));
                }
                else
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(string.Format(translations.id_not_valid, idStr)));
                }
            }
            catch
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(string.Format(translations.error_retrieving_guild_with_id, idStr)));
            }
        }

        [SlashCommand("logs", "Shows the lastest log file")]
        [DescriptionLocalization(Localization.Spanish, "Muestra el último archivo de log")]
        public async Task Logs(InteractionContext ctx)
        {
            await ctx.DeferAsync();
            
            FileInfo? log = Common.GetNewestFile(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "logs")));
            if (log != null)
            {
                using FileStream fs = File.Open(log.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); ;

                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .WithContent(translations.lastest_log_file)
                    .AddFile(log.Name, fs));

                fs.Close();
            }
            else
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                {
                    Title = translations.error,
                    Description = translations.no_logs_found,
                    Color = DiscordColor.Red
                }));
            }
        }

        [SlashCommand("poweroff", "Turn off the bot")]
        [NameLocalization(Localization.Spanish, "apagar")]
        [DescriptionLocalization(Localization.Spanish, "Apaga el bot")]
        public async Task Shutdown(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(translations.shutting_down).AsEphemeral(true));
            Environment.Exit(0);
        }
    }
}