The Puma Programming Language Project

The goal of this project is to write an LLVM compiler front-end for the Puma programming language. Puma is a new programming language that is safe, organized and maintainable. It is also readable, reliable and efficient.

Documents:
The specifications and other documents are found in the doc folder.  


Programming Language Development
In 2023, the foundation was laid for an exciting new programming language with the creation of the Puma programming language — a language designed with safety, organization, and maintainability at its core without compromising on performance. However, Puma isn’t just another new language. It’s a practical tool for writing cleaner code and fostering greater consistency across software development teams.

Key Features
•	Clean, simplified syntax with features to organize the code
•	Support for both object-oriented and procedural paradigms, giving developers flexibility
•	HTML window rendering through expressive library calls
•	Efficient handling of UTF-8 Unicode characters and strings for globalized applications
•	A thoughtfully designed, developer-friendly standard library
•	Ownership-based memory management model for safety without a garbage collector
•	Dynamic generics that adapt to your needs without sacrificing organization

Advanced Capabilities
•	Enforces one type definition per file for better project organization
•	Single-type with multi-trait inheritance structure for composability
•	Base types provide default behavior increasing maintainability
•	Default non-nullable references to eliminate null-pointer issues
•	Optionally nullable references when needed
•	Nullable references dereferencing from within a statement that checks for null
•	Direct access to low-level bit manipulation
•	Native support for fixed-point and floating-point precision
•	Full range of primitives: integers, Booleans, and more
•	Support for mutable and immutable variables for safe concurrency
•	An exception handling system that catches all exception

Compiler Development	
In 2024, development began on the Puma compiler — a three-phase quest to bring the language to life. 
- Phase one focuses on building a translator that converts Puma code into C/C++, enabling rapid prototyping and integration. 
- In phase two, the compiler becomes self-hosting, getting rewritten entirely in the Puma language. 
- The final phase replaces C/C++ as an intermediate language with direct generation of LLVM IR for streamlined performance and advanced tooling integration. 
The Puma compiler will evolve from here.

Standard Library
A comprehensive standard library will accompany the compiler, designed for both power and simplicity. With an emphasis on ease of use, the library will feature intuitive APIs and smart defaults—streamlining the most common use cases. Whether you're building tools or applications, Puma’s standard library will help you do more with less effort.
