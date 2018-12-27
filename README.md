# COBEREC Generator

This is COBEREC Generator project - a tool aiming at generating **COrrect REadable and BEautiful Code**, with priority on the correctness.


## GraphQL Schema

Currently it's capable of translating GraphQL schema into C# immutable classes, so you can declare your domain very easily without the boilerplate. As an intermediate representation, we use a simple representation of the schema, so you can also fairly easily consume it and create db schema, user interfaces or whatever yourself. Well, simply if you don't care about correctness in edge cases or have a simple and robust backend...

## CSharp genenerator

Our C# generator is based on the awesome ILSpy decompiler which helps us produce produce quite nice looking code that always represents what we intended in our semantic tree. C# is a very complex language and it would be very hard without this backend.
