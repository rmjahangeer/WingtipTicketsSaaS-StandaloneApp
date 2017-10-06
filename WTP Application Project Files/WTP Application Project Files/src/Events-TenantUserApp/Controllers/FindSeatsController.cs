﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Events_Tenant.Common.Interfaces;
using Events_Tenant.Common.Models;
using Events_TenantUserApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Events_TenantUserApp.Controllers
{
    [Route("{tenant}/FindSeats")]
    public class FindSeatsController : BaseController
    {
        #region Private varibles

        private readonly ITenantRepository _tenantRepository;
        private readonly IStringLocalizer<FindSeatsController> _localizer;
        private readonly ILogger _logger;

        #endregion

        #region Constructor

        public FindSeatsController(ITenantRepository tenantRepository, IStringLocalizer<FindSeatsController> localizer, IStringLocalizer<BaseController> baseLocalizer, ILogger<FindSeatsController> logger, IConfiguration configuration) : base(baseLocalizer, tenantRepository, configuration)
        {
            _tenantRepository = tenantRepository;
            _localizer = localizer;
            _logger = logger;
        }

        #endregion


        [Route("FindSeats")]
        public async Task<ActionResult> FindSeats(string tenant, int eventId)
        {
            try
            {
                if (eventId != 0)
                {
                    var tenantDetails = _tenantRepository.GetVenueByName(tenant).Result;
                    if (tenantDetails != null)
                    {
                        SetTenantConfig(tenantDetails.VenueId);

                        var eventDetails = await _tenantRepository.GetEvent(eventId, tenantDetails.VenueId);

                        if (eventDetails != null)
                        {
                            var eventSections = await _tenantRepository.GetEventSections(eventId, tenantDetails.VenueId);
                            var seatSectionIds = eventSections.Select(i => i.SectionId).ToList();

                            var seatSections = await _tenantRepository.GetSections(seatSectionIds, tenantDetails.VenueId);
                            if (seatSections != null)
                            {
                                var ticketsSold = await _tenantRepository.GetTicketsSold(seatSections[0].SectionId, eventId, tenantDetails.VenueId);

                                FindSeatViewModel viewModel = new FindSeatViewModel
                                {
                                    EventDetails = eventDetails,
                                    SeatSections = seatSections,
                                    SeatsAvailable = (seatSections[0].SeatRows * seatSections[0].SeatsPerRow) - ticketsSold
                                };

                                return View(viewModel);
                            }
                        }
                    }
                    else
                    {
                        return View("TenantError", tenant);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "FindSeats failed for tenant {tenant} and event {eventId}", tenant, eventId);
            }
            return RedirectToAction("Index", "Events", new { tenant });
        }

        [Route("GetAvailableSeats")]
        public async Task<ActionResult> GetAvailableSeats(string tenant, int sectionId, int eventId)
        {
            try
            {
                var tenantDetails = _tenantRepository.GetVenueByName(tenant).Result;
                if (tenantDetails != null)
                {
                    SetTenantConfig(tenantDetails.VenueId);

                    var sectionDetails = await _tenantRepository.GetSection(sectionId, tenantDetails.VenueId);
                    var totalNumberOfSeats = sectionDetails.SeatRows * sectionDetails.SeatsPerRow;
                    var ticketsSold = await _tenantRepository.GetTicketsSold(sectionId, eventId, tenantDetails.VenueId);

                    var availableSeats = totalNumberOfSeats - ticketsSold;
                    return Content(availableSeats.ToString());
                }
                else
                {
                    return View("TenantError", tenant);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "GetAvailableSeats failed for tenant {tenant} and event {eventId}", tenant, eventId);
                return Content("0");
            }
        }


        [HttpPost]
        [Route("PurchaseTickets")]
        public async Task<ActionResult> PurchaseTickets(string tenant, string eventId, string customerId, string ticketPrice, string ticketCount, string sectionId)
        {
            try
            {
                bool purchaseResult = false;
                int numberOfTickets = Convert.ToInt32(ticketCount);

                if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(customerId) || string.IsNullOrEmpty(ticketPrice) || string.IsNullOrEmpty(ticketCount))
                {
                    var message = _localizer["Enter quantity"];
                    DisplayMessage(message, "Confirmation");
                    return RedirectToAction("Index", "Events", new { tenant });
                }

                var ticketPurchaseModel = new TicketPurchaseModel
                {
                    CustomerId = Convert.ToInt32(customerId),
                    PurchaseTotal = Convert.ToDecimal(ticketPrice)
                };

                var tenantDetails = _tenantRepository.GetVenueByName(tenant).Result;
                if (tenantDetails != null)
                {
                    SetTenantConfig(tenantDetails.VenueId);

                    var latestPurchaseTicketId = await _tenantRepository.GetNumberOfTicketPurchases(tenantDetails.VenueId);
                    ticketPurchaseModel.TicketPurchaseId = latestPurchaseTicketId + 1;

                    var purchaseTicketId = await _tenantRepository.AddTicketPurchase(ticketPurchaseModel, tenantDetails.VenueId);

                    var ticketModel = new List<TicketModel>();
                    for (var i = 0; i < numberOfTickets; i++)
                    {
                        ticketModel.Add(new TicketModel
                        {
                            SectionId = Convert.ToInt32(sectionId),
                            EventId = Convert.ToInt32(eventId),
                            TicketPurchaseId = purchaseTicketId,
                            RowNumber = Convert.ToInt32(sectionId) + Convert.ToInt32(eventId) + purchaseTicketId, // ensures that the ticket purchased  row number is always unique
                            SeatNumber = i + 1
                        });
                    }
                    purchaseResult = await _tenantRepository.AddTicket(ticketModel, tenantDetails.VenueId);

                    if (purchaseResult)
                    {
                        var successMessage = _localizer[$"You have successfully purchased {ticketCount} ticket(s)."];
                        DisplayMessage(successMessage, "Confirmation");
                    }
                    else
                    {
                        var failureMessage = _localizer["Failed to purchase tickets."];
                        DisplayMessage(failureMessage, "Error");
                    }
                }
                else
                {
                    return View("TenantError", tenant);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "Purchase tickets failed for tenant {tenant} and event {eventId}", tenant, eventId);
            }
            return RedirectToAction("Index", "Events", new { tenant });
        }
    }
}
