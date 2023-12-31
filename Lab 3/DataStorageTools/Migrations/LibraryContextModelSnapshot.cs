﻿// <auto-generated />
using System;
using DataStorageTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DataStorageTools.Migrations
{
    [DbContext(typeof(LibraryContext))]
    partial class LibraryContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.14")
                .HasAnnotation("Proxies:ChangeTracking", false)
                .HasAnnotation("Proxies:CheckEquality", false)
                .HasAnnotation("Proxies:LazyLoading", true);

            modelBuilder.Entity("DataStorageTools.DetectedObject", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Class")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<double>("Confidence")
                        .HasColumnType("REAL");

                    b.Property<int?>("ProcessedImageId")
                        .HasColumnType("INTEGER");

                    b.Property<double>("XMax")
                        .HasColumnType("REAL");

                    b.Property<double>("XMin")
                        .HasColumnType("REAL");

                    b.Property<double>("YMax")
                        .HasColumnType("REAL");

                    b.Property<double>("YMin")
                        .HasColumnType("REAL");

                    b.HasKey("Id");

                    b.HasIndex("ProcessedImageId");

                    b.ToTable("DetectedObjects");
                });

            modelBuilder.Entity("DataStorageTools.ProcessedImage", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("Height")
                        .HasColumnType("INTEGER");

                    b.Property<byte[]>("Image")
                        .IsRequired()
                        .HasColumnType("BLOB");

                    b.Property<string>("ImagePath")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Width")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("ProcessedImages");
                });

            modelBuilder.Entity("DataStorageTools.DetectedObject", b =>
                {
                    b.HasOne("DataStorageTools.ProcessedImage", null)
                        .WithMany("DetectedObjects")
                        .HasForeignKey("ProcessedImageId");
                });

            modelBuilder.Entity("DataStorageTools.ProcessedImage", b =>
                {
                    b.Navigation("DetectedObjects");
                });
#pragma warning restore 612, 618
        }
    }
}
