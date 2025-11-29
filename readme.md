### Introduction
This repository contains materials for "A Quick Intro to HTMX" presentation.

**⚠️NOTE⚠️**: this repository was not meant to be a full fledged HTMX tutorial. It is just a set of exercises to explore HTMX capabilities and hypermedia principles in web applications.

### Context
In order to keep the exercises simple and focused on HTMX capabilities and hypermedia principles, there are some constraints and prerequisites:
* Razor was not used and interpolated, multiline strings were used instead to create HTML content in the backend


### Topics covered
It covers three topics:
* basic intro to HTMX principles (hypermedia) by exploring its syntax and capabilities through a simple exercise called [0-quick-intro-to-htmx](./0-quick-intro-to-htmx/readme.md)
* exploring what capabilities are provided when we organize our web application around hypermedia principles using HTMX through a more complex exercise called [1-json-to-hypermedia](./1-json-to-hypermedia/readme.md)
* exploring how to deal with high interaction elements like web maps in a hypermedia application using HTMX through an exercise called [2-islands-of-interactivity](./2-islands-of-interactivity/readme.md)

### Working with exercises
Exercises [0-quick-intro-to-htmx](./0-quick-intro-to-htmx/readme.md), [1-json-to-hypermedia](./1-json-to-hypermedia/readme.md) have two subdirectories: `exercise` and `solution`. The `exercise` folder contains starter code for the exercise while the `solution` folder contains the final solution code.

**⚠️NOTE⚠️**: if you don't want to implement it yourself, you can always peek into the `solution` folder to see the final code.

#### Peeking solutions

There are two scripts provided to help you work with the exercises:
* `./check-solution.sh <exercise-folder-name>`: This script will copy the solution files into the exercise folder so that you can the final solution using your favorite git client

**⚠️NOTE⚠️**: This will overwrite any changes you have made in the exercise folder!

* `./reset-exercise.sh <exercise-folder-name>`: This script will reset the exercise folder to its original state by copying the files from the `./solutions/<exercise-folder-name>/starter` folder into the exercise folder.

**⚠️NOTE⚠️**: this does hard reset and all changes are lost.