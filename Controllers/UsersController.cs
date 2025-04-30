using Aton_task.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Aton_task.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private const string CACHE_KEY = "UsersCache";
        private static readonly object _lock = new();
        private readonly IMemoryCache _cache;

        public UsersController(IMemoryCache cache)
        {
            _cache = cache;
        }

        private List<User> UsersList => _cache.Get<List<User>>(CACHE_KEY)!;

        #region CREATE
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult CreateUser([FromBody] UserDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            lock (_lock)
            {
                if (UsersList.Any(u => u.Login == dto.Login))
                    return BadRequest("Login is already taken");

                var newUser = new User
                {
                    Guid = Guid.NewGuid(),
                    Login = dto.Login,
                    Password = dto.Password,
                    Name = dto.Name,
                    Gender = dto.Gender,
                    Birthday = dto.Birthday,
                    Admin = dto.Admin,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = User.Identity!.Name!
                };

                UsersList.Add(newUser);
                return CreatedAtAction(nameof(GetUserByGuid), new { guid = newUser.Guid }, newUser);
            }
        }
        #endregion

        #region UPDATE
        [HttpPut("{guid}/profile")]
        [Authorize]
        public IActionResult UpdateProfile(Guid guid, [FromBody] UpdateProfileDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            lock (_lock)
            {
                var user = UsersList.FirstOrDefault(u => u.Guid == guid);
                if (user == null) return NotFound();

                if (!User.IsInRole("Admin") && User.Identity?.Name != user.Login)
                    return Forbid("Access denied");

                if (user.RevokedOn != null)
                    return BadRequest("User is revoked");

                user.Name = dto.Name;
                user.Gender = dto.Gender;
                user.Birthday = dto.Birthday;
                UpdateModificationFields(user);

                return NoContent();
            }
        }

        [HttpPut("{guid}/password")]
        [Authorize]
        public IActionResult UpdatePassword(Guid guid, [FromBody] UpdatePasswordDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            lock (_lock)
            {
                var user = UsersList.FirstOrDefault(u => u.Guid == guid);
                if (user == null) return NotFound();

                if (!User.IsInRole("Admin"))
                {
                    if (User.Identity?.Name != user.Login)
                        return Forbid();

                    if (user.Password != dto.OldPassword)
                        return BadRequest("Mistake in old password");
                }

                if (user.RevokedOn != null)
                    return BadRequest("User is revoked");

                user.Password = dto.NewPassword;
                UpdateModificationFields(user);

                return NoContent();
            }
        }

        [HttpPut("{guid}/login")]
        [Authorize]
        public IActionResult UpdateLogin(Guid guid, [FromBody] UpdateLoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            lock (_lock)
            {
                var user = UsersList.FirstOrDefault(u => u.Guid == guid);
                if (user == null) return NotFound();

                if (!User.IsInRole("Admin") && User.Identity?.Name != user.Login)
                    return Forbid();

                if (user.RevokedOn != null)
                    return BadRequest("User is revoked");

                if (UsersList.Any(u => u.Login == dto.NewLogin))
                    return BadRequest("Login  is already taken");

                user.Login = dto.NewLogin;
                UpdateModificationFields(user);

                return NoContent();
            }
        }
        #endregion

        #region READ
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult GetAllActiveUsers()
        {
            var users = UsersList
                .Where(u => u.RevokedOn == null)
                .OrderBy(u => u.CreatedOn)
                .Select(u => new UserInfoDto(u))
                .ToList();

            return Ok(users);
        }

        [HttpGet("{guid}")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetUserByGuid(Guid guid)
        {
            var user = UsersList.FirstOrDefault(u => u.Guid == guid);
            if (user == null) return NotFound();

            return Ok(new UserInfoDto(user));
        }

        [HttpGet("by-login/{login}")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetUserByLogin(string login)
        {
            var user = UsersList.FirstOrDefault(u => u.Login == login);
            if (user == null) return NotFound();

            return Ok(new UserInfoDto(user));
        }

        [HttpPost("authenticate")]
        public IActionResult Authenticate([FromBody] LoginDto dto)
        {
            var user = UsersList.FirstOrDefault(u =>
                u.Login == dto.Login &&
                u.Password == dto.Password);

            if (user == null || user.RevokedOn != null)
                return Unauthorized();

            return Ok(new
            {
                user.Guid,
                user.Login,
                user.Name,
                user.Admin
            });
        }

        [HttpGet("older-than/{age}")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetUsersOlderThan(int age)
        {
            var cutoffDate = DateTime.UtcNow.AddYears(-age);
            var users = UsersList
                .Where(u => u.Birthday.HasValue && u.Birthday.Value <= cutoffDate)
                .Select(u => new UserInfoDto(u))
                .ToList();

            return Ok(users);
        }
        #endregion

        #region DELETE
        [HttpDelete("{guid}")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteUser(Guid guid, [FromQuery] bool softDelete = true)
        {
            lock (_lock)
            {
                var user = UsersList.FirstOrDefault(u => u.Guid == guid);
                if (user == null) return NotFound();

                if (softDelete)
                {
                    user.RevokedOn = DateTime.UtcNow;
                    user.RevokedBy = User.Identity!.Name;
                    UpdateModificationFields(user);
                }
                else
                {
                    UsersList.Remove(user);
                }

                return NoContent();
            }
        }

        [HttpPost("{guid}/restore")]
        [Authorize(Roles = "Admin")]
        public IActionResult RestoreUser(Guid guid)
        {
            lock (_lock)
            {
                var user = UsersList.FirstOrDefault(u => u.Guid == guid);
                if (user == null) return NotFound();

                user.RevokedOn = null;
                user.RevokedBy = null;
                UpdateModificationFields(user);

                return NoContent();
            }
        }
        #endregion

        #region Helpers
        private void UpdateModificationFields(User user)
        {
            user.ModifiedOn = DateTime.UtcNow;
            user.ModifiedBy = User.Identity!.Name;
        }
        #endregion
    }
}