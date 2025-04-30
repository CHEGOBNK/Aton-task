using System.ComponentModel.DataAnnotations;

namespace Aton_task.Models
{
    public class UserDto
    {
        [Required]
        [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Login can't contain special symbols")]
        public string Login { get; set; } = null!;

        [Required]
        [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Password can't contain special symbols")]
        public string Password { get; set; } = null!;

        [Required]
        [RegularExpression(@"^[а-яА-ЯёЁa-zA-Z]+$", ErrorMessage = "Name can't contain special symbols")]
        public string Name { get; set; } = null!;

        [Range(0, 2)]
        public int Gender { get; set; }

        public DateTime? Birthday { get; set; }

        public bool Admin { get; set; }
    }
    public class UpdateProfileDto
    {
        [Required]
        [RegularExpression(@"^[а-яА-ЯёЁa-zA-Z]+$", ErrorMessage = "Name can't contain special symbols")]
        public string Name { get; set; } = null!;

        [Range(0, 2)]
        public int Gender { get; set; }

        public DateTime? Birthday { get; set; }
    }
    public class UpdateLoginDto
    {
        [Required]
        [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Login can't contain special symbols")]
        public string NewLogin { get; set; } = null!;
    }

    public class LoginDto
    {
        [Required]
        public string Login { get; set; } = null!;

        [Required]
        public string Password { get; set; } = null!;
    }

    public class UpdatePasswordDto
    {
        [Required]
        [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Password can't contain special symbols")]
        public string OldPassword { get; set; } = null!;

        [Required]
        [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Password can't contain special symbols")]
        public string NewPassword { get; set; } = null!;
    }
}