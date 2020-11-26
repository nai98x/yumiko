﻿using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System.Threading.Tasks;
using System.Collections.Generic;
using DSharpPlus.Entities;
using System;
using GraphQL.Client.Http;
using GraphQL;
using GraphQL.Client.Serializer.Newtonsoft;
using System.Linq;
using System.Configuration;
using DSharpPlus.Interactivity.Extensions;
using YumikoBot.Data_Access_Layer;

namespace Discord_Bot.Modulos
{
    public class Juegos : BaseCommandModule
    {
        private readonly FuncionesAuxiliares funciones = new FuncionesAuxiliares();
        private readonly GraphQLHttpClient graphQLClient = new GraphQLHttpClient("https://graphql.anilist.co", new NewtonsoftJsonSerializer());

        [Command("quizC"), Aliases("adivinaelpersonaje"), Description("Empieza el juego de adivina el personaje."), RequireGuild]
        public async Task QuizCharactersGlobal(CommandContext ctx, [Description("Para activar modo megu escribe -m o -megu")]string modoMegu = null)
        {
            bool meguMode = false;
            if (modoMegu == "-m" || modoMegu == "-megu")
                meguMode = true;
            var interactivity = ctx.Client.GetInteractivity();
            SettingsJuego settings = await funciones.InicializarJuego(ctx, interactivity);
            if (settings.Ok)
            {
                int rondas = settings.Rondas;
                string dificultadStr = settings.Dificultad;
                int iterIni = settings.IterIni;
                int iterFin = settings.IterFin;
                DiscordEmbed embebido = new DiscordEmbedBuilder
                {
                    Title = "Adivina el personaje",
                    Description = $"Sesión iniciada por {ctx.User.Mention}",
                    Color = funciones.GetColor()
                }.AddField("Rondas", $"{rondas}").AddField("Dificultad", $"{dificultadStr}");
                await ctx.RespondAsync(embed: embebido).ConfigureAwait(false);
                List<Character> characterList = new List<Character>();
                Random rnd = new Random();
                List<UsuarioJuego> participantes = new List<UsuarioJuego>();
                DiscordMessage mensaje = await ctx.RespondAsync($"Obteniendo personajes...").ConfigureAwait(false);
                string query = "query($pagina : Int){" +
                        "   Page(page: $pagina){" +
                        "       characters(sort:";
                query += settings.Orden;
                query +="){" +
                        "           siteUrl," +
                        "           name{" +
                        "               first," +
                        "               last," +
                        "               full" +
                        "           }," +
                        "           image{" +
                        "               large" +
                        "           }" +
                        "       }" +
                        "   }" +
                        "}";
                for (int i = iterIni; i <= iterFin; i++)
                {
                    var request = new GraphQLRequest
                    {
                        Query = query,
                        Variables = new
                        {
                            pagina = i
                        }
                    };
                    try
                    {
                        var data = await graphQLClient.SendQueryAsync<dynamic>(request);
                        foreach (var x in data.Data.Page.characters)
                        {
                            characterList.Add(new Character()
                            {
                                Image = x.image.large,
                                NameFull = x.name.full,
                                NameFirst = x.name.first,
                                NameLast = x.name.last,
                                SiteUrl = x.siteUrl
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        DiscordMessage msg;
                        switch (ex.Message)
                        {
                            default:
                                msg = await ctx.RespondAsync($"Error inesperado").ConfigureAwait(false);
                                break;
                        }
                        await Task.Delay(3000);
                        await ctx.Message.DeleteAsync("Auto borrado de yumiko");
                        await msg.DeleteAsync("Auto borrado de yumiko");
                        return;
                    }
                }
                await mensaje.DeleteAsync("Auto borrado de Yumiko");
                int lastRonda;
                for (int ronda = 1; ronda <= rondas; ronda++)
                {
                    lastRonda = ronda;
                    int random = funciones.GetNumeroRandom(0, characterList.Count - 1);
                    Character elegido = characterList[random];
                    if (meguMode)
                    {
                        await ctx.RespondAsync("ATENTOOOOS");
                        await Task.Delay(funciones.GetNumeroRandom(100, 3000));
                    }
                    await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Gold,
                        Title = "Adivina el personaje",
                        Description = $"Ronda {ronda} de {rondas}",
                        ImageUrl = elegido.Image
                    }).ConfigureAwait(false);
                    var msg = await interactivity.WaitForMessageAsync
                        (xm => (xm.Channel == ctx.Channel) &&
                        (xm.Content.ToLower().Trim() == elegido.NameFull.ToLower().Trim() || xm.Content.ToLower().Trim() == elegido.NameFirst.ToLower().Trim() || (elegido.NameLast != null && xm.Content.ToLower().Trim() == elegido.NameLast.ToLower().Trim())) || (xm.Content.ToLower() == "cancelar" && xm.Author == ctx.User)
                        , TimeSpan.FromSeconds(Convert.ToDouble(ConfigurationManager.AppSettings["GuessTimeGames"])));
                    if (!msg.TimedOut)
                    {
                        if (msg.Result.Author == ctx.User && msg.Result.Content.ToLower() == "cancelar")
                        {
                            await ctx.RespondAsync($"El juego ha sido cancelado por **{ctx.User.Username}#{ctx.User.Discriminator}**").ConfigureAwait(false);
                            await funciones.GetResultados(ctx, participantes, lastRonda, settings.Dificultad, "personaje");
                            return;
                        }
                        DiscordMember acertador = await ctx.Guild.GetMemberAsync(msg.Result.Author.Id);
                        UsuarioJuego usr = participantes.Find(x => x.Usuario == msg.Result.Author);
                        if (usr != null)
                        {
                            usr.Puntaje++;
                        }
                        else
                        {
                            participantes.Add(new UsuarioJuego()
                            {
                                Usuario = msg.Result.Author,
                                Puntaje = 1
                            });
                        }
                        await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = $"¡**{acertador.DisplayName}** ha acertado!",
                            Description = $"El nombre es: [{elegido.NameFull}]({elegido.SiteUrl})",
                            Color = DiscordColor.Green
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = "¡Nadie ha acertado!",
                            Description = $"El nombre era: [{elegido.NameFull}]({elegido.SiteUrl})",
                            Color = DiscordColor.Red
                        }).ConfigureAwait(false);
                    }
                    characterList.Remove(characterList[random]);
                }
                await funciones.GetResultados(ctx, participantes, rondas, settings.Dificultad, "personaje");
            }
            else
            {
                var error = await ctx.RespondAsync(settings.MsgError).ConfigureAwait(false);
            }
        }

        [Command("quizA"), Aliases("adivinaelanime"), Description("Empieza el juego de adivina el anime."), RequireGuild]
        public async Task QuizAnimeGlobal(CommandContext ctx, [Description("Para activar modo megu escribe -m o -megu")]string modoMegu = null)
        {
            bool meguMode = false;
            if (modoMegu == "-m" || modoMegu == "-megu")
                meguMode = true;
            var interactivity = ctx.Client.GetInteractivity();
            SettingsJuego settings = await funciones.InicializarJuego(ctx, interactivity);
            if (settings.Ok)
            {
                int rondas = settings.Rondas;
                string dificultadStr = settings.Dificultad;
                int iterIni = settings.IterIni;
                int iterFin = settings.IterFin;
                DiscordEmbed embebido = new DiscordEmbedBuilder
                {
                    Title = "Adivina el anime",
                    Description = $"Sesión iniciada por {ctx.User.Mention}",
                    Color = funciones.GetColor()
                }.AddField("Rondas", $"{rondas}").AddField("Dificultad", $"{dificultadStr}");
                await ctx.RespondAsync(embed: embebido).ConfigureAwait(false);
                Random rnd = new Random();
                List<UsuarioJuego> participantes = new List<UsuarioJuego>();
                DiscordMessage mensaje = await ctx.RespondAsync($"Obteniendo personajes...").ConfigureAwait(false);
                var characterList = new List<Character>();
                string query = "query($pagina : Int){" +
                        "   Page(page: $pagina){" +
                        "       characters(sort: ";
                query += settings.Orden;
                query += "){" +
                        "           siteUrl," +
                        "           name{" +
                        "               full" +
                        "           }," +
                        "           image{" +
                        "               large" +
                        "           }," +
                        "           media(type:ANIME){" +
                        "               nodes{" +
                        "                   title{" +
                        "                       romaji," +
                        "                       english" +
                        "                   }," +
                        "                   siteUrl" +
                        "               }" +
                        "           }" +
                        "       }" +
                        "   }" +
                        "}";
                for (int i = iterIni; i <= iterFin; i++)
                {
                    var request = new GraphQLRequest
                    {
                        Query = query,
                        Variables = new
                        {
                            pagina = i
                        }
                    };
                    try
                    {
                        var data = await graphQLClient.SendQueryAsync<dynamic>(request);
                        foreach (var x in data.Data.Page.characters)
                        {
                            Character c = new Character()
                            {
                                Image = x.image.large,
                                NameFull = x.name.full,
                                SiteUrl = x.siteUrl,
                                Animes = new List<Anime>()
                            };
                            foreach (var y in x.media.nodes)
                            {
                                string titleEnglish = y.title.english;
                                string titleRomaji = y.title.romaji;
                                c.Animes.Add(new Anime()
                                {
                                    TitleEnglish = funciones.QuitarCaracteresEspeciales(titleEnglish),
                                    TitleRomaji = funciones.QuitarCaracteresEspeciales(titleRomaji),
                                    SiteUrl = y.siteUrl
                                });
                            }
                            if (c.Animes.Count() > 0)
                            {
                                characterList.Add(c);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DiscordMessage msg;
                        switch (ex.Message)
                        {
                            default:
                                msg = await ctx.RespondAsync($"Error inesperado").ConfigureAwait(false);
                                break;
                        }
                        await Task.Delay(3000);
                        await ctx.Message.DeleteAsync("Auto borrado de yumiko");
                        await msg.DeleteAsync("Auto borrado de yumiko");
                        return;
                    }
                }
                await mensaje.DeleteAsync("Auto borrado de Yumiko");
                int lastRonda;
                for (int ronda = 1; ronda <= rondas; ronda++)
                {
                    lastRonda = ronda;
                    int random = funciones.GetNumeroRandom(0, characterList.Count - 1);
                    Character elegido = characterList[random];
                    if (meguMode)
                    {
                        await ctx.RespondAsync("ATENTOOOOS");
                        await Task.Delay(funciones.GetNumeroRandom(100, 3000));
                    }
                    await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Gold,
                        Title = $"Adivina el anime del personaje",
                        Description = $"Ronda {ronda} de {rondas}",
                        ImageUrl = elegido.Image
                    }).ConfigureAwait(false);
                    var msg = await interactivity.WaitForMessageAsync
                        (xm => (xm.Channel == ctx.Channel) &&
                        ((xm.Content.ToLower() == "cancelar" && xm.Author == ctx.User) ||
                        (elegido.Animes.Find(x => x.TitleEnglish != null && x.TitleEnglish.ToLower().Trim() == xm.Content.ToLower().Trim()) != null) ||
                        (elegido.Animes.Find(x => x.TitleRomaji != null && x.TitleRomaji.ToLower().Trim() == xm.Content.ToLower().Trim()) != null)),
                        TimeSpan.FromSeconds(Convert.ToDouble(ConfigurationManager.AppSettings["GuessTimeGames"])));
                    string descAnimes = $"Los animes de [{elegido.NameFull}]({elegido.SiteUrl}) son:\n\n";
                    foreach (Anime anim in elegido.Animes)
                    {
                        descAnimes += $"- [{anim.TitleRomaji}]({anim.SiteUrl})\n";
                    }
                    if (!msg.TimedOut)
                    {
                        if (msg.Result.Author == ctx.User && msg.Result.Content.ToLower() == "cancelar")
                        {
                            await ctx.RespondAsync($"El juego ha sido cancelado por **{ctx.User.Username}#{ctx.User.Discriminator}**").ConfigureAwait(false);
                            await funciones.GetResultados(ctx, participantes, lastRonda, settings.Dificultad, "anime");
                            return;
                        }
                        DiscordMember acertador = await ctx.Guild.GetMemberAsync(msg.Result.Author.Id);
                        UsuarioJuego usr = participantes.Find(x => x.Usuario == msg.Result.Author);
                        if (usr != null)
                        {
                            usr.Puntaje++;
                        }
                        else
                        {
                            participantes.Add(new UsuarioJuego()
                            {
                                Usuario = msg.Result.Author,
                                Puntaje = 1
                            });
                        }
                        await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = $"¡**{acertador.DisplayName}** ha acertado!",
                            Description = descAnimes,
                            Color = DiscordColor.Green
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = "¡Nadie ha acertado!",
                            Description = descAnimes,
                            Color = DiscordColor.Red
                        }).ConfigureAwait(false);
                    }
                    characterList.Remove(characterList[random]);
                }
                await funciones.GetResultados(ctx, participantes, rondas, settings.Dificultad, "anime");
            }
            else
            {
                var error = await ctx.RespondAsync(settings.MsgError).ConfigureAwait(false);
            }
        }

        [Command("statsC"), Aliases("estadisticaspersonajes"), Description("Estadisticas de adivina el personaje."), RequireGuild]
        public async Task EstadisticasAdivinaPersonaje(CommandContext ctx)
        {
            LeaderboardPersonajes leaderboardPjs = new LeaderboardPersonajes();
            DiscordEmoji emoji;

            List<StatsJuego> resFacil = leaderboardPjs.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Fácil");
            string facil = "";
            int pos = 0;
            int lastScore = 0;
            foreach (StatsJuego jugador in resFacil)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if(miembro != null)
                {
                    if (lastScore != jugador.PorcentajeAciertos)
                        pos++;
                    switch (pos)
                    {
                        case 1:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":first_place:");
                            facil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 2:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":second_place:");
                            facil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 3:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":third_place:");
                            facil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        default:
                            facil += $"#{pos} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                    }
                    lastScore = jugador.PorcentajeAciertos;
                }
            }

            List<StatsJuego> resMedia = leaderboardPjs.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Media");
            string media = "";
            pos = 0;
            lastScore = 0;
            foreach (StatsJuego jugador in resMedia)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    if (lastScore != jugador.PorcentajeAciertos)
                        pos++;
                    switch (pos)
                    {
                        case 1:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":first_place:");
                            media += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 2:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":second_place:");
                            media += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 3:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":third_place:");
                            media += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        default:
                            media += $"#{pos} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                    }
                    lastScore = jugador.PorcentajeAciertos;
                }
            }

            List<StatsJuego> resDificil = leaderboardPjs.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Dificil");
            string dificil = "";
            pos = 0;
            lastScore = 0;
            foreach (StatsJuego jugador in resDificil)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    if (lastScore != jugador.PorcentajeAciertos)
                        pos++;
                    switch (pos)
                    {
                        case 1:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":first_place:");
                            dificil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 2:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":second_place:");
                            dificil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 3:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":third_place:");
                            dificil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        default:
                            dificil += $"#{pos} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                    }
                    lastScore = jugador.PorcentajeAciertos;
                }
            }

            List<StatsJuego> resExtremo = leaderboardPjs.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Extremo");
            string extremo = "";
            pos = 0;
            lastScore = 0;
            foreach (StatsJuego jugador in resExtremo)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    if (lastScore != jugador.PorcentajeAciertos)
                        pos++;
                    switch (pos)
                    {
                        case 1:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":first_place:");
                            extremo += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 2:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":second_place:");
                            extremo += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 3:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":third_place:");
                            extremo += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        default:
                            extremo += $"#{pos} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                    }
                    lastScore = jugador.PorcentajeAciertos;
                }
            }

            List<StatsJuego> resKusan = leaderboardPjs.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Kusan");
            string kusan = "";
            pos = 0;
            lastScore = 0;
            foreach (StatsJuego jugador in resKusan)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    if (lastScore != jugador.PorcentajeAciertos)
                        pos++;
                    switch (pos)
                    {
                        case 1:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":first_place:");
                            kusan += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 2:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":second_place:");
                            kusan += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 3:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":third_place:");
                            kusan += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        default:
                            kusan += $"#{pos} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                    }
                    lastScore = jugador.PorcentajeAciertos;
                }
            }

            var builder = new DiscordEmbedBuilder
            {
                Title = "Estadisticas - Adivina el personaje",
                Footer = funciones.GetFooter(ctx),
                Color = funciones.GetColor()
            };
            if (resFacil.Count > 0)
                builder.AddField("Dificultad Fácil", facil);
            if (resMedia.Count > 0)
                builder.AddField("Dificultad Media", media);
            if (resDificil.Count > 0)
                builder.AddField("Dificultad Dificil", dificil);
            if (resExtremo.Count > 0)
                builder.AddField("Dificultad Extremo", extremo);
            if (resKusan.Count > 0)
                builder.AddField("Dificultad Kusan", kusan);
            await ctx.RespondAsync(embed: builder);

            await ctx.Message.DeleteAsync("Auto borrado de yumiko");
        }

        [Command("statsA"), Aliases("estadisticasanimes"), Description("Estadisticas de adivina el anime."), RequireGuild]
        public async Task EstadisticasAdivinaAnime(CommandContext ctx)
        {
            LeaderboardAnimes leaderboardAns = new LeaderboardAnimes();
            DiscordEmoji emoji;

            List<StatsJuego> resFacil = leaderboardAns.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Fácil");
            string facil = "";
            int pos = 0;
            int lastScore = 0;
            foreach (StatsJuego jugador in resFacil)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    if (lastScore != jugador.PorcentajeAciertos)
                        pos++;
                    switch (pos)
                    {
                        case 1:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":first_place:");
                            facil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 2:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":second_place:");
                            facil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 3:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":third_place:");
                            facil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        default:
                            facil += $"#{pos} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                    }
                    lastScore = jugador.PorcentajeAciertos;
                }
            }

            List<StatsJuego> resMedia = leaderboardAns.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Media");
            string media = "";
            pos = 0;
            lastScore = 0;
            foreach (StatsJuego jugador in resMedia)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    if (lastScore != jugador.PorcentajeAciertos)
                        pos++;
                    switch (pos)
                    {
                        case 1:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":first_place:");
                            media += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 2:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":second_place:");
                            media += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 3:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":third_place:");
                            media += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        default:
                            media += $"#{pos} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                    }
                    lastScore = jugador.PorcentajeAciertos;
                }
            }

            List<StatsJuego> resDificil = leaderboardAns.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Dificil");
            string dificil = "";
            pos = 0;
            lastScore = 0;
            foreach (StatsJuego jugador in resDificil)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    if (lastScore != jugador.PorcentajeAciertos)
                        pos++;
                    switch (pos)
                    {
                        case 1:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":first_place:");
                            dificil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 2:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":second_place:");
                            dificil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 3:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":third_place:");
                            dificil += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        default:
                            dificil += $"#{pos} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                    }
                    lastScore = jugador.PorcentajeAciertos;
                }
            }

            List<StatsJuego> resExtremo = leaderboardAns.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Extremo");
            string extremo = "";
            pos = 0;
            lastScore = 0;
            foreach (StatsJuego jugador in resExtremo)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    if (lastScore != jugador.PorcentajeAciertos)
                        pos++;
                    switch (pos)
                    {
                        case 1:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":first_place:");
                            extremo += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 2:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":second_place:");
                            extremo += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 3:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":third_place:");
                            extremo += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        default:
                            extremo += $"#{pos} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                    }
                    lastScore = jugador.PorcentajeAciertos;
                }
            }

            List<StatsJuego> resKusan = leaderboardAns.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Kusan");
            string kusan = "";
            pos = 0;
            lastScore = 0;
            foreach (StatsJuego jugador in resKusan)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    if (lastScore != jugador.PorcentajeAciertos)
                        pos++;
                    switch (pos)
                    {
                        case 1:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":first_place:");
                            kusan += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 2:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":second_place:");
                            kusan += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        case 3:
                            emoji = DiscordEmoji.FromName(ctx.Client, ":third_place:");
                            kusan += $"{emoji} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                        default:
                            kusan += $"#{pos} - **{miembro.Username}#{miembro.Discriminator}** - Aciertos: **{jugador.PorcentajeAciertos}%** - Partidas: **{jugador.PartidasTotales}**\n";
                            break;
                    }
                    lastScore = jugador.PorcentajeAciertos;
                }
            }

            var builder = new DiscordEmbedBuilder
            {
                Title = "Estadisticas - Adivina el anime",
                Footer = funciones.GetFooter(ctx),
                Color = funciones.GetColor()
            };
            if (resFacil.Count > 0)
                builder.AddField("Dificultad Fácil", facil);
            if (resMedia.Count > 0)
                builder.AddField("Dificultad Media", media);
            if (resDificil.Count > 0)
                builder.AddField("Dificultad Dificil", dificil);
            if (resExtremo.Count > 0)
                builder.AddField("Dificultad Extremo", extremo);
            if (resKusan.Count > 0)
                builder.AddField("Dificultad Kusan", kusan);
            await ctx.RespondAsync(embed: builder);

            await ctx.Message.DeleteAsync("Auto borrado de yumiko");
        }
    }
}