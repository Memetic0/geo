import { bindable, customElement } from 'aurelia';
import * as L from 'leaflet';

interface IncidentSummary {
  id: string;
  type: string;
  latitude: number;
  longitude: number;
  severity: string;
  state: string;
  location: string;
  description: string;
  createdAt: string;
  sensorStationId: string;
  responderId: string | null;
}

@customElement('incident-map')
export class IncidentMap {
  @bindable incidents: IncidentSummary[] = [];
  @bindable selectedIncidentId: string | null = null;
  @bindable hoveredIncidentId: string | null = null;
  @bindable onIncidentClick?: (incident: IncidentSummary) => void;
  @bindable onMapClick?: (event: { lat: number; lng: number }) => void;

  private mapContainer!: HTMLElement;
  private map!: L.Map;
  private markers: Map<string, L.Marker> = new Map();

  attached() {
    this.initializeMap();
  }

  detached() {
    if (this.map) {
      this.map.remove();
    }
  }

  private initializeMap() {
    // Initialize Leaflet map centered on London
    this.map = L.map(this.mapContainer).setView([51.505, -0.09], 11);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors',
      maxZoom: 19
    }).addTo(this.map);

    // Handle map clicks for creating new incidents
    this.map.on('click', (e: L.LeafletMouseEvent) => {
      this.onMapClick?.({ lat: e.latlng.lat, lng: e.latlng.lng });
    });
  }

  incidentsChanged() {
    this.updateMarkers();
  }

  selectedIncidentIdChanged(newId: string | null) {
    this.updateMarkerStyles();
  }

  hoveredIncidentIdChanged(newId: string | null) {
    this.updateMarkerStyles();
  }

  private updateMarkerStyles() {
    this.markers.forEach((marker, id) => {
      const isSelected = id === this.selectedIncidentId;
      const isHovered = id === this.hoveredIncidentId;
      const incident = this.incidents.find(i => i.id === id);
      if (incident) {
        const icon = this.createMarkerIcon(incident.type, incident.severity, isSelected, isHovered);
        marker.setIcon(icon);
      }
    });
  }

  private updateMarkers() {
    // Remove markers for incidents that no longer exist
    const currentIds = new Set(this.incidents.map(i => i.id));
    this.markers.forEach((marker, id) => {
      if (!currentIds.has(id)) {
        marker.remove();
        this.markers.delete(id);
      }
    });

    // Add or update markers for current incidents
    this.incidents.forEach(incident => {
      const existing = this.markers.get(incident.id);
      const isSelected = incident.id === this.selectedIncidentId;
      const isHovered = incident.id === this.hoveredIncidentId;

      if (existing) {
        // Update existing marker position and icon
        existing.setLatLng([incident.latitude, incident.longitude]);
        existing.setIcon(this.createMarkerIcon(incident.type, incident.severity, isSelected, isHovered));
        existing.setPopupContent(this.buildPopupContent(incident));
      } else {
        // Create new marker
        const icon = this.createMarkerIcon(incident.type, incident.severity, isSelected, isHovered);
        const marker = L.marker([incident.latitude, incident.longitude], { icon })
          .addTo(this.map)
          .bindPopup(this.buildPopupContent(incident));

        marker.on('click', () => {
          this.onIncidentClick?.(incident);
        });

        this.markers.set(incident.id, marker);
      }
    });
  }

  private createMarkerIcon(type: string, severity: string, isSelected: boolean, isHovered: boolean = false): L.DivIcon {
    const emoji = this.getTypeIcon(type);
    const severityClass = severity.toLowerCase();
    const selectedClass = isSelected ? 'selected' : '';
    const hoveredClass = isHovered ? 'hovered' : '';

    return L.divIcon({
      html: `<div class="incident-marker ${severityClass} ${selectedClass} ${hoveredClass}">
               <div class="marker-circle">
                 <span class="marker-icon">${emoji}</span>
               </div>
             </div>`,
      className: '',
      iconSize: [40, 40],
      iconAnchor: [20, 40],
      popupAnchor: [0, -40]
    });
  }

  private getTypeIcon(type: string): string {
    const icons: Record<string, string> = {
      'TrafficCongestion': '🚗',
      'RoadAccident': '🚨',
      'RoadClosure': '🚧',
      'VehicleBreakdown': '🔧',
      'Roadwork': '👷',
      'PublicTransportDelay': '🚌',
      'ParkingViolation': '🅿️',
      'SignalMalfunction': '🚦',
      'PedestrianIncident': '🚶',
      'StreetFlooding': '🌊'
    };
    return icons[type] || '📍';
  }

  private buildPopupContent(incident: IncidentSummary): string {
    const emoji = this.getTypeIcon(incident.type);
    return `
      <div class="incident-popup">
        <div class="popup-header">
          <span class="popup-emoji">${emoji}</span>
          <strong>${this.getIncidentTypeName(incident.type)}</strong>
        </div>
        <div class="popup-content">
          <p><strong>ID:</strong> ${incident.id}</p>
          <p><strong>Severity:</strong> <span class="incident-badge severity-${incident.severity.toLowerCase()}">${incident.severity}</span></p>
          <p><strong>State:</strong> ${incident.state}</p>
          <p><strong>Location:</strong> ${incident.latitude.toFixed(2)}, ${incident.longitude.toFixed(2)}</p>
          <p><strong>Sensor:</strong> ${incident.sensorStationId}</p>
          ${incident.responderId ? `<p><strong>Responder:</strong> ${incident.responderId}</p>` : ''}
          <button class="btn-popup-details" onclick="window.location.href='#/incidents/${incident.id}'">
            View Details
          </button>
        </div>
      </div>
    `;
  }

  private getIncidentTypeName(typeValue: string): string {
    const typeNames: Record<string, string> = {
      'TrafficCongestion': 'Traffic Congestion',
      'RoadAccident': 'Road Accident',
      'RoadClosure': 'Road Closure',
      'VehicleBreakdown': 'Vehicle Breakdown',
      'Roadwork': 'Roadwork',
      'PublicTransportDelay': 'Public Transport Delay',
      'ParkingViolation': 'Parking Violation',
      'SignalMalfunction': 'Signal Malfunction',
      'PedestrianIncident': 'Pedestrian Incident',
      'StreetFlooding': 'Street Flooding'
    };
    return typeNames[typeValue] || typeValue;
  }

  public resetZoom() {
    if (this.map) {
      this.map.setView([51.505, -0.09], 11);
    }
  }

  public zoomToIncident(lat: number, lng: number) {
    if (this.map) {
      this.map.setView([lat, lng], 16);
    }
  }

  public showIncidentPopup(incidentId: string) {
    const marker = this.markers.get(incidentId);
    if (marker) {
      marker.openPopup();
    }
  }
}
