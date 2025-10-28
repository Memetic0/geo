import { bindable, customElement } from 'aurelia';

export interface IncidentSummary {
  id: string;
  type: string;
  state: string;
  severity: string;
  latitude: number;
  longitude: number;
  sensorStationId: string;
  assignedResponderId?: string;
  raisedAt: string;
}

@customElement('incident-table-rows')
export class IncidentTableRows {
  @bindable incidents: IncidentSummary[] = [];
  @bindable selectedIncidentId: string | null = null;
  @bindable onRowHover!: (incident: IncidentSummary) => void;
  @bindable onRowLeave!: () => void;
  @bindable onRowClick!: (incident: IncidentSummary) => void;
  @bindable openSeverityModal!: (incident: IncidentSummary) => void;
  @bindable validateIncident!: (incident: IncidentSummary) => void;
  @bindable assignResponder!: (incident: IncidentSummary) => void;
  @bindable beginMitigation!: (incident: IncidentSummary) => void;
  @bindable beginMonitoring!: (incident: IncidentSummary) => void;
  @bindable resolveIncident!: (incident: IncidentSummary) => void;

  public getStateProgress(state: string): number {
    const stateMap: Record<string, number> = {
      'Detected': 16,
      'Acknowledged': 32,
      'Validated': 48,
      'Mitigating': 68,
      'Monitoring': 84,
      'Resolved': 100
    };
    return stateMap[state] || 0;
  }

  public getIncidentTypeName(typeValue: number | string): string {
    if (typeof typeValue === 'string') return typeValue;
    
    const typeNames = [
      'Traffic Congestion', 'Road Accident', 'Road Closure',
      'Vehicle Breakdown', 'Roadwork', 'Public Transport Delay',
      'Parking Violation', 'Signal Malfunction', 'Pedestrian Incident', 'Street Flooding'
    ];
    return typeNames[typeValue] || 'Unknown';
  }

  public getTypeIcon(type: string | number): string {
    const typeStr = typeof type === 'number' ? this.getIncidentTypeName(type) : type;
    const iconMap: Record<string, string> = {
      'TrafficCongestion': '🚗',
      'Traffic Congestion': '🚗',
      'RoadAccident': '💥',
      'Road Accident': '💥',
      'RoadClosure': '🚧',
      'Road Closure': '🚧',
      'VehicleBreakdown': '🔧',
      'Vehicle Breakdown': '🔧',
      'Roadwork': '👷',
      'PublicTransportDelay': '🚌',
      'Public Transport Delay': '🚌',
      'ParkingViolation': '🅿️',
      'Parking Violation': '🅿️',
      'SignalMalfunction': '🚦',
      'Signal Malfunction': '🚦',
      'PedestrianIncident': '🚶',
      'Pedestrian Incident': '🚶',
      'StreetFlooding': '💧',
      'Street Flooding': '💧'
    };
    return iconMap[typeStr] || '📍';
  }

  public formatTimeAgo(dateString: string): string {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    return `${diffDays}d ago`;
  }

  public canBeginMitigation(incident: IncidentSummary): boolean {
    return incident.state === 'Validated';
  }

  public canValidate(incident: IncidentSummary): boolean {
    return incident.state === 'Detected' || incident.state === 'Acknowledged';
  }

  public canBeginMonitoring(incident: IncidentSummary): boolean {
    return incident.state === 'Mitigating';
  }

  public canResolve(incident: IncidentSummary): boolean {
    return incident.state === 'Monitoring';
  }

  public handleRowClick(incident: IncidentSummary, event: MouseEvent): void {
    // Prevent row click if clicking on a button
    const target = event.target as HTMLElement;
    if (target.closest('button')) {
      return;
    }
    
    // Call the bound onRowClick handler
    if (this.onRowClick) {
      this.onRowClick(incident);
    }
  }
}
