using Coberec.CoreLib;
using System;

namespace Test
{
	public sealed class Email : IEquatable<Email>
	{
		public string Value {
			get;
		}

		private static ValidationErrors ValidateObject(Email obj)
		{
			return BasicValidators.NotNull(obj.Value).Nest("value");
		}

		private Email(NoNeedForValidationSentinel _, string value)
		{
			Value = value;
		}

		public Email(string value)
			: this(default(NoNeedForValidationSentinel), value)
		{
			ValidateObject(this).ThrowErrors("Could not initialize Email due to validation errors");
		}

		public static ValidationResult<Email> Create(string value)
		{
			Email email = new Email(default(NoNeedForValidationSentinel), value);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(email), email);
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}

		public bool Equals(Email b)
		{
			return (object)this == b || Value == b.Value;
		}

		public static bool operator ==(Email a, Email b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(Email a, Email b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			Email b2;
			return (object)(b2 = (b as Email)) != null && Equals(b2);
		}
	}
	public sealed class User : IEquatable<User>
	{
		public string Name {
			get;
		}

		public Email Email {
			get;
		}

		private static ValidationErrors ValidateObject(User obj)
		{
			return ValidationErrors.Join(BasicValidators.NotNull(obj.Name).Nest("name"), BasicValidators.NotNull(obj.Email).Nest("email"));
		}

		private User(NoNeedForValidationSentinel _, string name, Email email)
		{
			Name = name;
			Email = email;
		}

		public User(string name, Email email)
			: this(default(NoNeedForValidationSentinel), name, email)
		{
			ValidateObject(this).ThrowErrors("Could not initialize User due to validation errors");
		}

		public static ValidationResult<User> Create(string name, Email email)
		{
			User user = new User(default(NoNeedForValidationSentinel), name, email);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(user), user);
		}

		public override int GetHashCode()
		{
			return (Name, Email).GetHashCode();
		}

		public bool Equals(User b)
		{
			return (object)this == b || (Name == b.Name && Email == b.Email);
		}

		public static bool operator ==(User a, User b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(User a, User b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			User b2;
			return (object)(b2 = (b as User)) != null && Equals(b2);
		}

		public ValidationResult<User> With(string name, Email email)
		{
			return (Name == name && Email == email) ? ValidationResult.Create(this) : Create(Name, Email);
		}

		public ValidationResult<User> With(OptParam<string> name = default(OptParam<string>), OptParam<Email> email = default(OptParam<Email>))
		{
			return With(name.ValueOrDefault(Name), email.ValueOrDefault(Email));
		}
	}
	public sealed class Robot : IEquatable<Robot>
	{
		public string Name {
			get;
		}

		private static ValidationErrors ValidateObject(Robot obj)
		{
			return BasicValidators.NotNull(obj.Name).Nest("name");
		}

		private Robot(NoNeedForValidationSentinel _, string name)
		{
			Name = name;
		}

		public Robot(string name)
			: this(default(NoNeedForValidationSentinel), name)
		{
			ValidateObject(this).ThrowErrors("Could not initialize Robot due to validation errors");
		}

		public static ValidationResult<Robot> Create(string name)
		{
			Robot robot = new Robot(default(NoNeedForValidationSentinel), name);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(robot), robot);
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		public bool Equals(Robot b)
		{
			return (object)this == b || Name == b.Name;
		}

		public static bool operator ==(Robot a, Robot b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(Robot a, Robot b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			Robot b2;
			return (object)(b2 = (b as Robot)) != null && Equals(b2);
		}

		public ValidationResult<Robot> With(string name)
		{
			return (Name == name) ? ValidationResult.Create(this) : Create(Name);
		}
	}
	public abstract class Entity : IEquatable<Entity>
	{
		public sealed class RobotCase : Entity
		{
			public Robot Item {
				get;
			}

			public RobotCase(Robot item)
			{
				Item = item;
			}

			public override int GetHashCode()
			{
				return ((object)Item).GetHashCode();
			}

			private protected override bool EqualsCore(Entity b)
			{
				RobotCase robotCase;
				return (robotCase = (b as RobotCase)) != null && Item == ((RobotCase)b).Item;
			}

			public override TResult Match<TResult>(Func<RobotCase, TResult> robot, Func<UserCase, TResult> user)
			{
				return robot(this);
			}
		}

		public sealed class UserCase : Entity
		{
			public User Item {
				get;
			}

			public UserCase(User item)
			{
				Item = item;
			}

			public override int GetHashCode()
			{
				return ((object)Item).GetHashCode();
			}

			private protected override bool EqualsCore(Entity b)
			{
				UserCase userCase;
				return (userCase = (b as UserCase)) != null && Item == ((UserCase)b).Item;
			}

			public override TResult Match<TResult>(Func<RobotCase, TResult> robot, Func<UserCase, TResult> user)
			{
				return user(this);
			}
		}

		private protected abstract bool EqualsCore(Entity b);

		public virtual bool Equals(Entity b)
		{
			return (object)this == b || EqualsCore(b);
		}

		public static bool operator ==(Entity a, Entity b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(Entity a, Entity b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			Entity b2;
			return (object)(b2 = (b as Entity)) != null && Equals(b2);
		}

		public abstract TResult Match<TResult>(Func<RobotCase, TResult> robot, Func<UserCase, TResult> user);

		public static Entity Robot(Robot item)
		{
			return new RobotCase(item);
		}

		public static Entity User(User item)
		{
			return new UserCase(item);
		}
	}
}
