using CourseRegistration;

var course = new CourseOffering(capacity: 2, requiredPrerequisite: "Intro");
var records = new InMemoryPrerequisiteRecords();
foreach (var id in new[] { "learner-1", "learner-2", "learner-3" }) records.Grant(id, "Intro");
var service = new EnrollmentService(course, records);

Console.WriteLine(service.Enroll("learner-1").Message);
Console.WriteLine(service.Enroll("learner-2").Message);
Console.WriteLine(service.Enroll("learner-3").Message);
Console.WriteLine(service.Enroll("learner-4").Message); // no prerequisite on record
Console.WriteLine(service.Drop("learner-1").Message);
Console.WriteLine($"Enrolled: {string.Join(", ", course.EnrolledLearners)}");
Console.WriteLine($"Waitlist: {string.Join(", ", course.Waitlist)}");
