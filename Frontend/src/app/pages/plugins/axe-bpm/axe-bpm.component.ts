import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EventsService } from '../../../core/services/events.service';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-axe-bpm',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './axe-bpm.component.html',
  styleUrl: './axe-bpm.component.scss'
})
export class AxeBpmComponent implements OnInit {

  eventType = '';
  loading = false;
  successMessage = '';
  errorMessage = '';
  projects: { id: string, name: string }[] = [];
  selectedProjectId = '';

  constructor(
    private eventsService: EventsService,
    private router: Router,
    private http: HttpClient
  ) {}

  ngOnInit(): void {
    this.http.get<any[]>(`${environment.apiUrl}/projects`).subscribe({
      next: (projects) => {
        this.projects = projects.map(p => ({ id: p.id, name: p.name }));
      },
      error: () => {
        this.errorMessage = 'Impossible de charger les projets.';
      }
    });
  }

  publishEvent(): void {
    if (!this.eventType) {
      this.errorMessage = 'Please enter event type.';
      return;
    }
    if (!this.selectedProjectId) {
      this.errorMessage = 'Please select a project.';
      return;
    }

    this.loading = true;
    this.successMessage = '';
    this.errorMessage = '';

    this.eventsService.publish('axeBPM', this.eventType, this.selectedProjectId).subscribe({
      next: () => {
        const projectName = this.projects.find(p => p.id === this.selectedProjectId)?.name || '';
        this.successMessage = `Event "${this.eventType}" published in project "${projectName}" !`;
        this.loading = false;
        this.eventType = '';
      },
      error: () => {
        this.errorMessage = 'Error while publishing event.';
        this.loading = false;
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/tools']);
  }
}