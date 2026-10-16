namespace KD.Avalonia.Rice.Demo;

/// <summary>One PlantUML sample per UML diagram type.</summary>
public static class UmlSamples
{
    public sealed record Sample(string Name, string Source)
    {
        public override string ToString() => Name;
    }

    public static IReadOnlyList<Sample> All { get; } =
    [
        new("Class", """
            @startuml
            title Online shop
            abstract class Product {
              -id : Guid
              #name : string
              +{abstract} Price() : decimal
            }
            class Book {
              +isbn : string
              +Price() : decimal
            }
            class Order {
              +created : DateTime
              +Total() : decimal
              +{static} Create(customer : Customer) : Order
            }
            class OrderLine {
              +quantity : int
            }
            interface IShippable {
              +Weight() : double
            }
            enum OrderState {
              New
              Paid
              Shipped
            }
            class Customer
            Product <|-- Book
            IShippable <|.. Book
            Order "1" *-- "1..*" OrderLine : contains
            OrderLine --> "1" Product
            Customer "1" o-- "*" Order : places >
            Order ..> OrderState
            note right of Order : Totals are cached\nuntil a line changes.
            @enduml
            """),
        new("Object", """
            @startuml
            object alice {
              name = "Alice"
              age = 31
            }
            object order42 {
              id = 42
              state = Paid
            }
            object line1 {
              quantity = 2
            }
            object book {
              title = "Dune"
            }
            alice --> order42 : places
            order42 *-- line1
            line1 --> book
            @enduml
            """),
        new("Package", """
            @startuml
            package "Presentation" {
              package Views
              package ViewModels
            }
            package "Domain" {
              package Model
              package Services
            }
            package Infrastructure
            Views ..> ViewModels
            ViewModels ..> Services
            Services ..> Model
            Infrastructure ..> Model
            @enduml
            """),
        new("Component", """
            @startuml
            package "Web" {
              [Browser UI] as UI
            }
            component "API Gateway" as GW {
              portin http
              portout grpc
            }
            [Order Service] as OS
            [Catalog Service] as CS
            database "Orders DB" as ODB
            interface "REST" as REST
            UI --> REST
            REST - http
            grpc --> OS
            GW --> CS
            OS --> ODB
            @enduml
            """),
        new("Composite structure", """
            @startuml
            ' rice:kind=compositestructure
            component Car {
              portin ignition
              rectangle "e : Engine" as e
              rectangle "t : Transmission" as t
              rectangle "w : Wheel [4]" as w
            }
            ignition --> e
            e --> t : torque
            t --> w
            @enduml
            """),
        new("Deployment", """
            @startuml
            node "Web server" as web <<device>> {
              artifact "shop.war" as war
            }
            node "App server" as app {
              component "Order service" as os
            }
            database "PostgreSQL" as db
            cloud "CDN" as cdn
            actor Customer
            Customer --> cdn : HTTPS
            cdn --> web
            web --> app : REST
            app --> db : JDBC
            @enduml
            """),
        new("Profile", """
            @startuml
            metaclass Class
            metaclass Property
            stereotype Entity {
              table : String
            }
            stereotype Column {
              name : String
              nullable : Boolean
            }
            Entity --|> Class
            Column --|> Property
            @enduml
            """),
        new("Use case", """
            @startuml
            left to right direction
            actor Customer
            actor "Payment provider" as PP
            rectangle "Online shop" {
              usecase "Browse catalog" as UC1
              usecase "Place order" as UC2
              usecase "Pay" as UC3
              usecase "Apply coupon" as UC4
            }
            Customer --> UC1
            Customer --> UC2
            UC2 .> UC3 : <<include>>
            UC4 .> UC2 : <<extend>>
            UC3 --> PP
            @enduml
            """),
        new("Activity", """
            @startuml
            |Customer|
            start
            :Put items in cart;
            :Check out;
            |Shop|
            if (Items in stock?) then (yes)
              :Reserve items;
              fork
                :Charge card;
              fork again
                :Prepare parcel;
              end fork
              :Ship order;
            else (no)
              :Notify customer;
              stop
            endif
            |Customer|
            :Receive parcel;
            note right: Signature needed
            stop
            @enduml
            """),
        new("State", """
            @startuml
            [*] --> Idle
            Idle --> Processing : order received
            state Processing {
              [*] --> Validating
              Validating --> Charging : valid
              Charging --> [*]
            }
            Processing --> Shipped : paid
            Processing --> Cancelled : error [retries > 3]
            Shipped --> [*]
            Cancelled --> [*]
            Idle : entry / reset timer
            @enduml
            """),
        new("Sequence", """
            @startuml
            autonumber
            actor User
            participant "Web app" as Web
            participant "Order service" as Orders
            database "Database" as DB
            User -> Web : Place order
            activate Web
            Web -> Orders ++ : create(order)
            Orders -> DB : INSERT order
            DB --> Orders : id
            alt payment ok
              Orders -> Orders : confirm()
              Orders --> Web : 201 Created
            else payment failed
              Orders --> Web : 402 Payment required
            end
            deactivate Orders
            Web --> User : Show result
            deactivate Web
            note over User, Web : The page polls\nfor status updates
            @enduml
            """),
        new("Communication", """
            @startuml
            ' rice:kind=communication
            object ":Customer" as c
            object ":Order" as o
            object ":Stock" as s
            object ":Payment" as p
            c -- o : 1: place() >
            o -- s : 2: reserve() >\n< 3: ok
            o -- p : 4: charge() >
            @enduml
            """),
        new("Timing", """
            @startuml
            robust "Web browser" as WB
            concise "User" as U
            clock "Clock" as clk with period 50
            binary "Busy" as B
            WB has Idle,Waiting,Processing

            @0
            U is Idle
            WB is Idle
            B is low

            @100
            U is Waiting
            WB is Processing
            B is high

            @300
            WB is Waiting
            B is low

            @450
            U is Idle
            WB is Idle

            WB@100 -> U@300 : response
            @enduml
            """),
        new("Interaction overview", """
            @startuml
            ' rice:kind=interactionoverview
            start
            :ref Authenticate;
            if (Authenticated?) then (yes)
              :ref Browse catalog;
              :ref Place order;
            else (no)
              :ref Show error;
            endif
            stop
            @enduml
            """),
    ];
}
