[![Nuget](https://img.shields.io/nuget/v/SharpSteer.svg?style=flat)](https://www.nuget.org/packages/SharpSteer)

SharpSteer is a C# port of OpenSteer. Like OpenSteer, the aim of SharpSteer is to help construct steering behaviors for autonomous characters in games and animation.

Like OpenSteer, SharpSteer provides a XNA-based application which demonstrates predefined steering behaviors. The user can quickly prototype, visualize, annotate and debug new steering behaviors by writing a plug-in for this Demo application.

This fork of SharpSteer includes:

 - Proper use of C# features such as extension methods to make the library easier to use.
 - Changes  to improve code quality/neatness.
 - Total separation of the demo and the library applications.
 - Some behaviours mentioned in the [original paper](http://www.red3d.com/cwr/papers/1999/gdc99steer.html) but never implemented in OpenSteer.
 - Good intentions to have 100% unit test coverage (lots of work needed here).
 - Modified to completely remove XNA dependency from the library

### Nuget

SharpSteer is [available](https://www.nuget.org/packages/SharpSteer/) as a nuget package.

```ps
$ dotnet add package SharpSteer
```

### Documentation

The original steering behaviours are documented [here](http://www.red3d.com/cwr/papers/1999/gdc99steer.html)
