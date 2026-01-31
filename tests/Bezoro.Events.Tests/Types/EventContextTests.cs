using Bezoro.Events.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.Types;

[TestSubject(typeof(EventContext<>))]
public static class EventContextTests
{
    public class Unit
    {
        [Fact]
        public void Constructor_ShouldStoreData()
        {
            var evt = new TestEventA(42);
            var ctx = new EventContext<TestEventA>(evt);
            ctx.Data.Value.Should().Be(42);
        }

        [Fact]
        public void Handled_ShouldDefaultToFalse()
        {
            var ctx = new EventContext<TestEventA>(new TestEventA(1));
            ctx.Handled.Should().BeFalse();
        }

        [Fact]
        public void Handled_WhenSetToTrue_ShouldBeTrue()
        {
            var ctx = new EventContext<TestEventA>(new TestEventA(1));
            ctx.Handled = true;
            ctx.Handled.Should().BeTrue();
        }

        [Fact]
        public void Data_ShouldPreserveAllFields()
        {
            var evt = new TestEventB("hello");
            var ctx = new EventContext<TestEventB>(evt);
            ctx.Data.Message.Should().Be("hello");
        }
    }
}
