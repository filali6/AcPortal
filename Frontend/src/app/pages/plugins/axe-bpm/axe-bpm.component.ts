import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EventsService } from '../../../core/services/events.service';

@Component({
  selector: 'app-axe-bpm',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './axe-bpm.component.html',
  styleUrl: './axe-bpm.component.scss'
})
export class AxeBpmComponent {

   
  eventType = '';
  loading = false;
  successMessage = '';
  errorMessage = '';

   
  constructor(
    private eventsService: EventsService,
    private router: Router
  ) {}

  publishEvent(): void {
    // Validation
    if (!this.eventType) {
      this.errorMessage = 'please enter event type ';
      return;
    }

    this.loading = true;
    this.successMessage = '';
    this.errorMessage = '';

    // Appel POST /api/events/publish
    this.eventsService.publish('axeBPM', this.eventType).subscribe({
      next: () => {
        this.successMessage = `event "${this.eventType}" published successfully !`;
        this.loading = false;
        this.eventType = '';
      },
      error: () => {
        this.errorMessage = 'error while publishing event   ';
        this.loading = false;
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/tools']);
  }
}