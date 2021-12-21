﻿// <auto-generated />
using System;
using FridgeBot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace FridgeBot.Migrations
{
    [DbContext(typeof(FridgeDbContext))]
    partial class FridgeDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.0");

            modelBuilder.Entity("FridgeBot.FridgeEntry", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<ulong>("ChannelId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("FridgeMessageId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("MessageId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ServerId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("MessageId");

                    b.HasIndex("ServerId", "ChannelId", "MessageId")
                        .IsUnique();

                    b.ToTable("Entries");
                });

            modelBuilder.Entity("FridgeBot.FridgeEntryEmote", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<ulong>("EmoteId")
                        .HasColumnType("INTEGER");

                    b.Property<Guid>("FridgeEntryId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("FridgeEntryId", "EmoteId")
                        .IsUnique();

                    b.ToTable("FridgeEntryEmote");
                });

            modelBuilder.Entity("FridgeBot.ServerEmote", b =>
                {
                    b.Property<ulong>("ServerId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("EmoteId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MaximumToRemove")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MinimumToAdd")
                        .HasColumnType("INTEGER");

                    b.HasKey("ServerId", "EmoteId");

                    b.ToTable("Emotes");
                });

            modelBuilder.Entity("FridgeBot.ServerFridge", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ChannelId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Servers");
                });

            modelBuilder.Entity("FridgeBot.FridgeEntry", b =>
                {
                    b.HasOne("FridgeBot.ServerFridge", "Server")
                        .WithMany("FridgeEntries")
                        .HasForeignKey("ServerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Server");
                });

            modelBuilder.Entity("FridgeBot.FridgeEntryEmote", b =>
                {
                    b.HasOne("FridgeBot.FridgeEntry", "FridgeEntry")
                        .WithMany("Emotes")
                        .HasForeignKey("FridgeEntryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("FridgeEntry");
                });

            modelBuilder.Entity("FridgeBot.ServerEmote", b =>
                {
                    b.HasOne("FridgeBot.ServerFridge", "Server")
                        .WithMany()
                        .HasForeignKey("ServerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Server");
                });

            modelBuilder.Entity("FridgeBot.FridgeEntry", b =>
                {
                    b.Navigation("Emotes");
                });

            modelBuilder.Entity("FridgeBot.ServerFridge", b =>
                {
                    b.Navigation("FridgeEntries");
                });
#pragma warning restore 612, 618
        }
    }
}
