# Rent tracker

Web application that allows to register multiple rent locations to keep track of the payments

![index](https://user-images.githubusercontent.com/248934/199085783-d9e49d36-cdc7-41f7-9331-db51b118272f.jpg)

## Expected functionality

- Web application that we can access from a computer or mobile device
- A property represents a house, land, room or department
- Allows to track multiple properties
- Each property can have multiple owners
- Each property can have a tenant at a time, but multiple over its lifetime
- Data should be stored in a centralized place, using the most simple technology (SQLite)
- It should be functional and not fancy
- It should be fast and responsive
- Ideally, we should provide reports over year, month regarding the rental payments

## Use cases / Phase - 1

1. Create an account with some role, initial roles are System Administrator, Owner, Tennant
2. Use the full-name as the account identity, validate uniqueness
3. Add a property, the data required for a property must include: location (GPS), surface in meters, nr of rooms, facilities included (bathroom, kitchen, garage, hot water, AC, backyard, security, doorbell), price and warranty
4. Update a property, here, the location can't change, the other data can be updated
5. Disable property, the property will not be removed, only will be usable when enabled again (data will be kept always)
6. Enable property, (data will be kept always)
7. Rent a property to a tenant
8. Close rental of property, when the rental is finished
9. Terminate rental of property, when something unexpected happened and the rental must be forcibly finished
10. Add rental payment
11. Update rental payment, this will not overwrite the prior payment, a new one must be created and the payment logs must be kept
12. We need to keep an history of the prices, warranties, because those can change in the future
13. The prices/costs can be stored in Bolivianos for the moment, but we must consider that in the future we would like to support USD as well
14. Reports, we will need reports about the properties, the rentals, payments, etc. Something in real-time would be enough with some nice graphs
15. There will be a default admin/admin user that will require a password change after first login, this user can set other users

## Technical specs

- Use dotnet-mvc with the latest LTS supported platform
- Use the standard stack, with the frontend I think the current approach is to write some sort of web assembly, check
- The sqlite DB must be optimized with some pragmas, check
- It's gonna be deployed as a docker image, the db and other uploaded files should be able to be mapped into a local folder
- Must be design-responsive, will need to work on desktop, mobile and tablet
- It must use standard CSS since it's quite capable now, no additional frameworks
- The design must be simple and clear, the users are gonna be regular
- Design the application for future changes, using migrations, good coding practices and keeping complexity controlled
- Consider that AIs will be used extensively in this project, define proper build guidelines, proper documentation and instructions for AI agents (AGENTS.md, copilot instructions, etc.)
- The project will be hosted in GitHub and built with Github actions, there's no need to place it in a registry yet, the build must result in a docker image we can download and load
