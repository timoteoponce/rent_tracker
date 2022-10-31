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

## Use cases

- Create an account with some role, initial roles are System Administrator, Owner, Tennant
- Use the email address as the account identity
- Add a property, the data required for a property must include: location (GPS), surface in meters, nr of rooms, facilities included (bathroom, kitchen, garage, hot water, AC, backyard, security, doorbell), price and warranty 

- Update a property, here, the location can't change, the other data can be updated
- Disable property, the property will not be removed, only will be usable when enabled again (data will be kept always)
- Enable property, (data will be kept always)
- Rent a property to a tennant
- Close rental of property, when the rental is finished
- Terminate rental of property, when something unexpected happened and the rental must be forcibly finished
- Add rental payment
- Update rental payment, this will not overwrite the prior payment, a new one must be created and the payment logs must be kept
