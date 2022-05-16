﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using RadioTracklistsOnSpotify.Services.DataSourceService;
using RadioTracklistsOnSpotify.Services.DataSourceService.Abstraction;
using RadioTracklistsOnSpotify.Services.DataSourceService.Configuration;

namespace AutomatedPlaylist.Tests.Services
{
    [TestFixture]
    public class RadioNowySwiatDirectDataSourceService
    {
        private IDataSourceService _sut;

        [SetUp]
        public void Setup()
        {
            var logger = Mock.Of<ILogger<RadioTracklistsOnSpotify.Services.DataSourceService.RadioNowySwiatDirectDataSourceService>>();
            var options = Options.Create(new DataSourceOptions
            {
                PlaylistEndpoint = "https://nowyswiat.online/playlista/?dzien=",
                DateFormat = "yyyy-MM-dd"
            });

            _sut = new RadioTracklistsOnSpotify.Services.DataSourceService.RadioNowySwiatDirectDataSourceService(logger, options);
        }

        [Test]
        public async Task GetPlaylistFor_FixedDate_ReturnsNotEmptyList()
        {
            //Arrange
            var date = new DateTime(2022, 02, 24);

            //Act
            var result = await _sut.GetPlaylistFor(date);

            //Assert
            Assert.That(result, Is.Not.Empty);
        }

        [Test]
        public async Task GetPlaylistFor_Tomorrow_ReturnsTodaysPlayList()
        {
            //Arrange
            var today = DateTime.Today;
            var todayList = await _sut.GetPlaylistFor(today);
            var tomorrowDate = today.AddDays(1);

            //Act
            var result = await _sut.GetPlaylistFor(tomorrowDate);

            //Assert
            Assert.That(result, Is.Not.Empty);
            Assert.That(result.Count, Is.EqualTo(todayList.Count));
            CollectionAssert.AreEquivalent(todayList, result);
        }
    }
}
