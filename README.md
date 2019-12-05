# OpsSecProject
A Project for Operations Security & Project (ITP391) Module 
## Team Members
* [Dominic Tay](https://github.com/k553)
* [Hansen Lim](https://github.com/hanlim83)
* [Yu Heng](https://github.com/Blitznoobz)

## Summary

>In this day and age, enterprises have many IT systems that the IT department, specifically the IT Security personnel must monitor for possible Indicators Of Compromise (IOCs) or Indicators Of Attack (IOAs) to avoid data breaches, bad reputation, denial of service, etc. 

Currently, enterprises are making use of Security Information and Event Management (SIEM) systems to monitor their IT systems. Although using SIEMs is better than manually monitoring the systems’ log files, they:
- Can be complex to set up
- Have antiquated licensing models (Usually by data ingested)
- Are limited in automation
- Are not scalable due to hardware limitations

Hence, our solution aims to combat these problems presented by traditional SIEMs through a cloud-based log analysis and visualization system and performing machine learning on the ingested log data.

## Features
- Authentication and Authorization
  - Rely on External Identity Provider Through OAuth 2.0 Protocol
  - Authorization by Group Assignment 
- Log Ingestion
  - Log Ingestion by Kinesis Firehose and Kinesis Agent
  - Log Data to be stored in S3 buckets
- Log Analysis
  - Using Machine Learning to check for anomalies
  - Automatic training and inference
  - Support looking at suspicious ip addresses or metric values
- Alerting
  - Pre-defined alerts set up for specifc log type
  - Alerts sent to user via email or SMS 
- Log Visualisation
  - Log Data can be visualised into pre-defined useful dashboards

## External Resources Used
- [Bootstrap](https://github.com/twbs/bootstrap) (Local JS & CSS when in Development, CDN when in Production)
   - [Start Boostrap 2 - Dashboard Template](https://github.com/BlackrockDigital/startbootstrap-sb-admin-2) 
   - [jQuery](https://github.com/jquery/jquery) 
      - [jQuery Vaildation](https://github.com/jquery-validation/jquery-validation) 
      - [jQuery Vaildation Unobtrusive](https://github.com/aspnet/jquery-validation-unobtrusive) 
      - [jQuery Easing](https://github.com/gdsmith/jquery.easing) 
   - [Popper](https://github.com/FezVrasta/popper.js)
- [Font Awesome Icons](https://fontawesome.com/)
- [Chart.js](https://www.chartjs.org/)
- [DataTables](https://datatables.net/) 

## Tools Used
- Visual Studio 2019
- ASP.NET Core 2.2 Framework
- ASP.NET Entity Framework Core (EF Core)
- Amazon Web Services (S3, RDS, EC2, EBS, SES, SNS, ELB, Sagemaker, Kinesis)
- [Json.NET](https://github.com/JamesNK/Newtonsoft.Json)
- [BCrypt.Net](https://github.com/BcryptNet/bcrypt.net)
