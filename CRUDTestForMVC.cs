using DogRallyMVC.Controllers;
using DogRallyMVC.Models;
using DogRallyMVC.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting; 
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRUDTestDogRallyMVC
{
    [TestClass]
    public class TracksControllerTests
    {
        private TracksController _controller;
        private DogRallyContext _context;
        private IPostTrackToAPI _postTrackToAPI;
        private IGetExercisesFromAPI _getExercisesFromAPI;
        private IHttpClientFactory _httpClientFactory;
        private ILogger<TracksController> _logger;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<DogRallyContext>()
                .UseInMemoryDatabase(databaseName: "TestDogRallyDatabase")
                .Options;
            _context = new DogRallyContext(options);

            // Seed the in-memory database
            SeedDatabase();

            _postTrackToAPI = new PostTrackToAPI(_context);
            _getExercisesFromAPI = new GetExercisesFromAPI(_context);
            _httpClientFactory = new HttpClientFactory();
            _logger = new LoggerFactory().CreateLogger<TracksController>();

            _controller = new TracksController(
                _logger,
                _postTrackToAPI,
                _httpClientFactory,
                _getExercisesFromAPI,
                null,
                null,
                null
            );

            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        private void SeedDatabase()
        {
            // Ensure the database is clean before seeding
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();

            var exercises = new List<Exercise>
            {
                new Exercise
                {
                    ExerciseID = 1, // Ensure IDs are set
                    ExerciseName = "Højresving",
                    ExerciseMovementEnumID = PaceEnum.Walk,
                    ExerciseSideShift = false,
                    ExerciseIllustrationPath = "/images/exercises/3.png",
                    ExerciseClassEnumID = ClassEnum.Beginner,
                    ExerciseSignNumber = 3,
                    ExercisePositionX = 10,
                    ExercisePositionY = 75
                },
                new Exercise
                {
                    ExerciseID = 2, // Ensure IDs are set
                    ExerciseName = "Venstresving",
                    ExerciseMovementEnumID = PaceEnum.Walk,
                    ExerciseSideShift = false,
                    ExerciseIllustrationPath = "/images/exercises/4.png",
                    ExerciseClassEnumID = ClassEnum.Beginner,
                    ExerciseSignNumber = 4,
                    ExercisePositionX = 10,
                    ExercisePositionY = 150
                }
            };

            var tracks = new List<Track>
            {
                new Track { TrackID = 1, TrackName = "Track 1", TrackDate = DateTime.Now, UserID = "user1" },
                new Track { TrackID = 2, TrackName = "Track 2", TrackDate = DateTime.Now, UserID = "user2" }
            };

            var trackExercises = new List<TrackExercise>
            {
                new TrackExercise { ForeignTrackID = 1, ForeignExerciseID = 1, TrackExercisePositionX = 10, TrackExercisePositionY = 20 },
                new TrackExercise { ForeignTrackID = 1, ForeignExerciseID = 2, TrackExercisePositionX = 30, TrackExercisePositionY = 40 },
                new TrackExercise { ForeignTrackID = 2, ForeignExerciseID = 1, TrackExercisePositionX = 15, TrackExercisePositionY = 25 },
                new TrackExercise { ForeignTrackID = 2, ForeignExerciseID = 2, TrackExercisePositionX = 35, TrackExercisePositionY = 45 }
            };

            _context.Exercises.AddRange(exercises);
            _context.Tracks.AddRange(tracks);
            _context.TrackExercises.AddRange(trackExercises);
            _context.SaveChanges();
        }

        [TestMethod]
        public async Task CreateTrack_Get_ReturnsViewWithViewModel()
        {
            // Act
            var result = await _controller.CreateTrack() as ViewResult;

            // Assert
            Assert.IsNotNull(result);
            var viewModel = result.Model as TrackExerciseViewModelDTO;
            Assert.IsNotNull(viewModel);
            Assert.AreEqual(2, viewModel.Exercises.Count);
        }

        [TestMethod]
        public async Task CreateTrack_Post_ReturnsRedirectToActionResult_WhenSuccess()
        {
            // Arrange
            var trackDTO = new TrackDTO { TrackID = 3, TrackName = "Track 3", TrackDate = System.DateTime.Now, UserID = "user1" };
            var exercises = new List<ExerciseDTO>
            {
                new ExerciseDTO { ExerciseID = 1, ExerciseName = "Exercise 1" },
                new ExerciseDTO { ExerciseID = 2, ExerciseName = "Exercise 2" }
            };

            var viewModel = new TrackExerciseViewModelDTO
            {
                Track = trackDTO,
                Exercises = exercises
            };

            // Mock session
            _controller.HttpContext.Session.SetString("JWTToken", "test-token");

            // Act
            var result = await _controller.CreateTrack(viewModel) as RedirectToActionResult;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Tracks", result.ActionName);
            Assert.AreEqual("MyTracks", result.RouteValues["type"]);
        }

        [TestMethod]
        public async Task CreateTrack_Post_ReturnsViewWithErrorMessage_WhenFailure()
        {
            // Arrange
            var trackDTO = new TrackDTO { TrackID = 3, TrackName = "Track 3", TrackDate = System.DateTime.Now, UserID = "user1" };
            var exercises = new List<ExerciseDTO>
            {
                new ExerciseDTO { ExerciseID = 1, ExerciseName = "Exercise 1" },
                new ExerciseDTO { ExerciseID = 2, ExerciseName = "Exercise 2" }
            };

            var viewModel = new TrackExerciseViewModelDTO
            {
                Track = trackDTO,
                Exercises = exercises
            };

            // Simulate a failure response by not setting the JWT token
            _controller.HttpContext.Session.Remove("JWTToken");

            // Act
            var result = await _controller.CreateTrack(viewModel) as ViewResult;

            // Assert
            Assert.IsNotNull(result);
            var modelState = result.ViewData.ModelState;
            Assert.IsTrue(modelState.ContainsKey(string.Empty));
        }

        private class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> _sessionStorage = new Dictionary<string, byte[]>();

            public IEnumerable<string> Keys => _sessionStorage.Keys;

            public string Id { get; } = Guid.NewGuid().ToString();

            public bool IsAvailable { get; } = true;

            public void Clear() => _sessionStorage.Clear();

            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public void Remove(string key) => _sessionStorage.Remove(key);

            public void Set(string key, byte[] value) => _sessionStorage[key] = value;

            public bool TryGetValue(string key, out byte[] value) => _sessionStorage.TryGetValue(key, out value);
        }

        private class PostTrackToAPI : IPostTrackToAPI
        {
            private readonly DogRallyContext _context;

            public PostTrackToAPI(DogRallyContext context)
            {
                _context = context;
            }

            public async Task<HttpResponseMessage> PostTrack(TrackExerciseViewModelDTO tevm, HttpClient client)
            {
                _context.Tracks.Add(new Track
                {
                    TrackID = tevm.Track.TrackID,
                    TrackName = tevm.Track.TrackName,
                    TrackDate = tevm.Track.TrackDate,
                    UserID = tevm.Track.UserID
                });

                foreach (var exercise in tevm.Exercises)
                {
                    _context.TrackExercises.Add(new TrackExercise
                    {
                        ForeignTrackID = tevm.Track.TrackID,
                        ForeignExerciseID = exercise.ExerciseID,
                        TrackExercisePositionX = exercise.ExercisePositionX,
                        TrackExercisePositionY = exercise.ExercisePositionY
                    });
                }

                await _context.SaveChangesAsync();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        private class GetExercisesFromAPI : IGetExercisesFromAPI
        {
            private readonly DogRallyContext _context;

            public GetExercisesFromAPI(DogRallyContext context)
            {
                _context = context;
            }

            public async Task<List<ExerciseDTO>> GetExercises(HttpClient client)
            {
                var exercises = await _context.Exercises.ToListAsync();
                return exercises.ConvertAll(e => new ExerciseDTO
                {
                    ExerciseID = e.ExerciseID,
                    ExerciseName = e.ExerciseName,
                    ExerciseMovementEnumID = e.ExerciseMovementEnumID,
                    ExerciseSideShift = e.ExerciseSideShift,
                    ExerciseIllustrationPath = e.ExerciseIllustrationPath,
                    ExerciseClassEnumID = e.ExerciseClassEnumID,
                    ExerciseSignNumber = e.ExerciseSignNumber,
                    ExercisePositionX = e.ExercisePositionX,
                    ExercisePositionY = e.ExercisePositionY
                });
            }
        }

        private class HttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name)
            {
                return new HttpClient();
            }
        }
    }
}